using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.TaskTrackers.Contracts.Generated;

namespace Throne.Api.TaskTrackers;

/// <summary>
/// Projects the provider-neutral <see cref="TaskTrackerCard"/> onto the wire DTO for the card browser.
/// Read-only view — no attachment identity, no availability (the browser has neither attach nor refresh).
/// Enriches the DTO with a per-request derived <c>web_url</c> composed by the tracker provider from the
/// saved connection (ADR-0052: non-authoritative, never persisted alongside the projection).
/// </summary>
public sealed class TaskTrackerCardDtoMapper(
    ITaskTrackerProviderRegistry providers,
    ITaskTrackerConnectionStore connections)
{
    public async Task<TaskTrackerCardDto> ToDtoAsync(string tracker, TaskTrackerCard card, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        ArgumentNullException.ThrowIfNull(card);
        var stored = await connections.GetAsync(tracker, ct);
        return Build(card, BuildWebUrl(tracker, card.CardId, stored));
    }

    public async Task<TaskTrackerBoardCardsResponse> ToResponseAsync(
        string tracker, IReadOnlyList<TaskTrackerCard> cards, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        ArgumentNullException.ThrowIfNull(cards);
        // The whole list belongs to one tracker — one connection read is enough for the entire response.
        var stored = await connections.GetAsync(tracker, ct);
        var items = new List<TaskTrackerCardDto>(cards.Count);
        foreach (var card in cards)
        {
            items.Add(Build(card, BuildWebUrl(tracker, card.CardId, stored)));
        }
        return new TaskTrackerBoardCardsResponse { Cards = items };
    }

    private string? BuildWebUrl(string tracker, string cardId, TaskTrackerStoredConnection? stored)
    {
        if (stored is null)
        {
            return null;
        }
        if (providers.GetByName(tracker) is not ITaskTrackerConnectionProvider provider)
        {
            return null;
        }
        return provider.BuildCardWebUrl(
            new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token), cardId);
    }

    private static TaskTrackerCardDto Build(TaskTrackerCard card, string? webUrl) => new()
    {
        Card_id = card.CardId,
        Board_id = card.BoardId,
        Column_id = card.ColumnId,
        Column_title = card.ColumnTitle,
        Text = card.Text,
        Updated_at = card.UpdatedAt,
        Archived = card.Archived,
        Card_version = card.RevisionTag,
        Web_url = card.WebUrl ?? webUrl,
    };
}
