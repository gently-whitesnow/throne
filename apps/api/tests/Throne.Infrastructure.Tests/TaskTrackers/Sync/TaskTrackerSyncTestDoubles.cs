using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Sync;

namespace Throne.Infrastructure.Tests.TaskTrackers.Sync;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>Hand-written sync provider used by the integration tests; replays a scripted single-card answer.</summary>
internal sealed class StubSyncProvider(string trackerKey = "kaiten") : ITaskTrackerSyncProvider
{
    public string TrackerKey { get; } = trackerKey;
    public string DisplayName => "Stub";

    public Func<string, TaskTrackerCard?>? GetCard { get; set; }
    public HashSet<string> ChildCardIds { get; } = new(StringComparer.Ordinal);
    public List<(string Parent, string Child)> Added { get; } = [];

    public Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection, string boardId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);

    public Task<TaskTrackerCard?> GetCardAsync(TaskTrackerConnectionDescriptor connection, string cardId, CancellationToken ct) =>
        Task.FromResult(GetCard?.Invoke(cardId));

    public Task<TaskTrackerCard> UpdateCardAsync(
        TaskTrackerConnectionDescriptor connection, string cardId, TaskTrackerCardPatch patch, CancellationToken ct) =>
        Task.FromResult(new TaskTrackerCard(cardId, "board-7", null, null, patch.Title ?? string.Empty, patch.Description, null, null, false));

    public Task<IReadOnlyList<string>> ListChildCardIdsAsync(
        TaskTrackerConnectionDescriptor connection, string parentCardId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>(ChildCardIds.ToList());

    public Task AddChildAsync(TaskTrackerConnectionDescriptor connection, string parentCardId, string childCardId, CancellationToken ct)
    {
        Added.Add((parentCardId, childCardId));
        return Task.CompletedTask;
    }

    public Task RemoveChildAsync(TaskTrackerConnectionDescriptor connection, string parentCardId, string childCardId, CancellationToken ct) =>
        Task.CompletedTask;
}
