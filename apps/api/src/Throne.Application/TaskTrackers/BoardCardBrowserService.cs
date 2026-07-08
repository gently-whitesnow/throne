using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Application.TaskTrackers;

/// <summary>
/// Read-only card-browser surface over a connected board (ADR-0052/0053): list a board's active cards
/// and read one card. Never touches intents. Resolves the connectable provider by tracker key (422
/// unsupported / 409 not-connected), then translates the provider-neutral
/// <see cref="TaskTrackerConnectionHealth"/> onto the ADR-0053 degradation surface (auth → 409, blocked
/// → 402, offline → 502) and feeds the outcome back to the persisted connection health — a browse hits
/// the tracker on real operator intent, so it is as honest a probe as attach/refresh.
/// </summary>
public sealed class BoardCardBrowserService(
    ITaskTrackerProviderRegistry registry,
    ITaskTrackerConnectionStore connections,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        string tracker, string boardId, CancellationToken ct)
    {
        var resolved = await ResolveAsync(tracker, ct);
        var now = clock.GetUtcNow();
        try
        {
            var cards = await resolved.Provider.ListBoardCardsAsync(resolved.Connection, boardId, ct);
            await RecordHealthAsync(tracker, TaskTrackerConnectionHealth.Connected, detail: null, now, ct);
            return cards;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TaskTrackerConnectionException ex)
        {
            await RecordHealthAsync(tracker, ex.Health, ex.Message, now, ct);
            throw MapFailure(tracker, ex);
        }
    }

    public async Task<TaskTrackerCard> GetBoardCardAsync(
        string tracker, string boardId, string cardId, CancellationToken ct)
    {
        var resolved = await ResolveAsync(tracker, ct);
        var now = clock.GetUtcNow();
        try
        {
            var card = await resolved.Provider.GetCardAsync(resolved.Connection, cardId, ct);
            await RecordHealthAsync(tracker, TaskTrackerConnectionHealth.Connected, detail: null, now, ct);
            // A card that resolved but sits on a different board is «not on this board» — 404, the same
            // outcome the operator sees for a vanished card, so the browser never renders a foreign card.
            if (card is null || !string.Equals(card.BoardId, boardId, StringComparison.Ordinal))
            {
                throw TaskTrackerFailures.CardNotFound(tracker, cardId);
            }
            return card;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TaskTrackerConnectionException ex)
        {
            await RecordHealthAsync(tracker, ex.Health, ex.Message, now, ct);
            throw MapFailure(tracker, ex);
        }
    }

    private async Task<CardTrackerConnection> ResolveAsync(string tracker, CancellationToken ct)
    {
        var provider = registry.GetByName(tracker) as ITaskTrackerConnectionProvider
            ?? throw TaskTrackerFailures.ProviderUnsupported(
                tracker,
                registry.AllProviders.OfType<ITaskTrackerConnectionProvider>().Select(p => p.TrackerKey));
        var stored = await connections.GetAsync(tracker, ct)
            ?? throw TaskTrackerFailures.ConnectionMissing(tracker);
        return new CardTrackerConnection(
            provider, new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token));
    }

    private static ApiException MapFailure(string tracker, TaskTrackerConnectionException ex) => ex.Health switch
    {
        TaskTrackerConnectionHealth.Auth => TaskTrackerFailures.ConnectionRejected(tracker, ex.Message),
        TaskTrackerConnectionHealth.Blocked => TaskTrackerFailures.ConnectionBlocked(tracker, ex.Message),
        _ => TaskTrackerFailures.UpstreamUnavailable(tracker, ex.Message),
    };

    private Task RecordHealthAsync(
        string tracker, TaskTrackerConnectionHealth health, string? detail, DateTimeOffset now, CancellationToken ct) =>
        connections.SaveHealthAsync(tracker, health, detail, now, ct);

    /// <summary>A resolved connectable provider paired with its per-call connection descriptor.</summary>
    private sealed record CardTrackerConnection(
        ITaskTrackerConnectionProvider Provider,
        TaskTrackerConnectionDescriptor Connection);
}
