using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers.Sync;

internal sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>In-memory <see cref="ITaskTrackerCardLinkStore"/> keyed by intent id.</summary>
internal sealed class FakeCardLinkStore : ITaskTrackerCardLinkStore
{
    private readonly Dictionary<string, CardSyncLink> _byIntent = new(StringComparer.Ordinal);

    public List<string> DeletedIntentIds { get; } = [];

    public void Seed(CardSyncLink link) => _byIntent[link.IntentId] = link;

    public Task<CardSyncLink?> GetByIntentAsync(string intentId, CancellationToken ct) =>
        Task.FromResult(_byIntent.GetValueOrDefault(intentId));

    public Task<CardSyncLink?> GetByCardAsync(string tracker, string boardId, string cardId, CancellationToken ct) =>
        Task.FromResult(_byIntent.Values.FirstOrDefault(l =>
            l.Card.Tracker == tracker && l.Card.BoardId == boardId && l.Card.CardId == cardId));

    public Task<IReadOnlyList<CardSyncLink>> ListByTrackerAsync(string tracker, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CardSyncLink>>(
            _byIntent.Values.Where(l => l.Card.Tracker == tracker).ToList());

    public Task<IReadOnlyList<CardSyncLink>> ListByBoardAsync(string tracker, string boardId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CardSyncLink>>(
            _byIntent.Values.Where(l => l.Card.Tracker == tracker && l.Card.BoardId == boardId).ToList());

    public Task SaveAsync(CardSyncLink link, CancellationToken ct)
    {
        _byIntent[link.IntentId] = link;
        return Task.CompletedTask;
    }

    public Task DeleteByIntentAsync(string intentId, CancellationToken ct)
    {
        _byIntent.Remove(intentId);
        DeletedIntentIds.Add(intentId);
        return Task.CompletedTask;
    }
}

/// <summary>Single-connection <see cref="ITaskTrackerConnectionStore"/>.</summary>
internal sealed class FakeConnectionStore : ITaskTrackerConnectionStore
{
    private readonly Dictionary<string, TaskTrackerStoredConnection> _byTracker = new(StringComparer.Ordinal);

    public void Seed(string tracker, TaskTrackerStoredConnection connection) => _byTracker[tracker] = connection;

    public Task<TaskTrackerStoredConnection?> GetAsync(string tracker, CancellationToken ct) =>
        Task.FromResult(_byTracker.GetValueOrDefault(tracker));

    public Task SaveConnectionAsync(string tracker, string baseUrl, string token, CancellationToken ct)
    {
        _byTracker[tracker] = new TaskTrackerStoredConnection(baseUrl, token, []);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string tracker, CancellationToken ct)
    {
        _byTracker.Remove(tracker);
        return Task.CompletedTask;
    }

    public Task SaveSelectionAsync(string tracker, IReadOnlyList<TaskTrackerBoardSelection> selection, CancellationToken ct) =>
        Task.CompletedTask;
}

/// <summary>Hand-written <see cref="ITaskTrackerSyncProvider"/> that records calls and replays scripted answers.</summary>
internal sealed class RecordingSyncProvider(string trackerKey = "kaiten") : ITaskTrackerSyncProvider
{
    public string TrackerKey { get; } = trackerKey;
    public string DisplayName => "Recording";

    public bool Throw { get; set; }
    public Func<string, TaskTrackerCard?>? GetCard { get; set; }
    public HashSet<string> ChildCardIds { get; } = new(StringComparer.Ordinal);

    public List<(string CardId, TaskTrackerCardPatch Patch)> Updated { get; } = [];
    public List<(string Parent, string Child)> Added { get; } = [];
    public List<(string Parent, string Child)> Removed { get; } = [];

    public Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection, string boardId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);

    public Task<TaskTrackerCard?> GetCardAsync(TaskTrackerConnectionDescriptor connection, string cardId, CancellationToken ct)
    {
        if (Throw)
        {
            throw new InvalidOperationException("boom");
        }

        return Task.FromResult(GetCard?.Invoke(cardId));
    }

    public Task<TaskTrackerCard> UpdateCardAsync(
        TaskTrackerConnectionDescriptor connection, string cardId, TaskTrackerCardPatch patch, CancellationToken ct)
    {
        if (Throw)
        {
            throw new InvalidOperationException("boom");
        }

        Updated.Add((cardId, patch));
        return Task.FromResult(new TaskTrackerCard(
            cardId, "board-7", null, null, patch.Title ?? string.Empty, patch.Description, null, null, false));
    }

    public Task<IReadOnlyList<string>> ListChildCardIdsAsync(
        TaskTrackerConnectionDescriptor connection, string parentCardId, CancellationToken ct)
    {
        if (Throw)
        {
            throw new InvalidOperationException("boom");
        }

        return Task.FromResult<IReadOnlyList<string>>(ChildCardIds.ToList());
    }

    public Task AddChildAsync(TaskTrackerConnectionDescriptor connection, string parentCardId, string childCardId, CancellationToken ct)
    {
        Added.Add((parentCardId, childCardId));
        return Task.CompletedTask;
    }

    public Task RemoveChildAsync(TaskTrackerConnectionDescriptor connection, string parentCardId, string childCardId, CancellationToken ct)
    {
        Removed.Add((parentCardId, childCardId));
        return Task.CompletedTask;
    }
}
