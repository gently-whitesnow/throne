using NSubstitute;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Attachments;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers.Attachments;

/// <summary>
/// Shared fakes + builders for the <see cref="CardAttachmentService"/> tests (split across the Attach /
/// Refresh-Detach suites). Uses an in-memory store so the idempotent re-attach path (existing → refresh
/// snapshot) is exercised end-to-end rather than mocked. Members are <c>internal</c> — a test helper is
/// not public API, and it keeps the type inside the maintainability public-member budget.
/// </summary>
internal sealed class CardAttachmentServiceFixture
{
    internal const string IntentIdValue = "intent-1";
    internal const string Tracker = "kaiten";
    internal const string BoardId = "10";
    internal const string CardId = "42";

    internal static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    public CardAttachmentServiceFixture()
    {
        Intents = Substitute.For<IIntentRepository>();
        Registry = Substitute.For<ITaskTrackerProviderRegistry>();
        Connections = Substitute.For<ITaskTrackerConnectionStore>();
        var resolver = new CardAttachmentResolver(Intents, Store, Registry, Connections);
        Service = new CardAttachmentService(resolver, Store, Connections, new PassthroughUnitOfWork(), new FixedClock(Now));
    }

    internal InMemoryCardAttachmentStore Store { get; } = new();

    internal IIntentRepository Intents { get; }

    internal ITaskTrackerProviderRegistry Registry { get; }

    internal ITaskTrackerConnectionStore Connections { get; }

    internal FakeConnectionProvider Provider { get; } = new();

    internal CardAttachmentService Service { get; }

    internal static AttachCardCommand AttachCommand(string cardId = CardId) =>
        new(IntentIdValue, Tracker, BoardId, cardId);

    internal void IntentExists(string intentId = IntentIdValue)
    {
        var intent = Intent.Restore(
            new IntentId(intentId), "x", IntentStatusNames.Work, 1, [], Now, Now);
        Intents.GetByIdAsync(Arg.Is<IntentId>(i => i.Value == intentId), Arg.Any<CancellationToken>())
            .Returns(intent);
    }

    internal void TrackerConnected(string tracker = Tracker)
    {
        Registry.GetByName(tracker).Returns(Provider);
        Connections.GetAsync(tracker, Arg.Any<CancellationToken>())
            .Returns(new TaskTrackerStoredConnection("https://acme.kaiten.ru", "tok", []));
    }

    internal void TrackerUnsupported(string tracker = Tracker) =>
        Registry.GetByName(tracker).Returns((ITaskTrackerProvider?)null);

    internal void TrackerNotConnected(string tracker = Tracker)
    {
        Registry.GetByName(tracker).Returns(Provider);
        Connections.GetAsync(tracker, Arg.Any<CancellationToken>())
            .Returns((TaskTrackerStoredConnection?)null);
    }

    /// <summary>Attach a live card, then arm the provider to return null so a follow-up refresh degrades.</summary>
    internal async Task<IntentCardAttachment> SeedAttachedAsync()
    {
        IntentExists();
        TrackerConnected();
        Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(Card(title: "Seed"));
        var attached = await Service.AttachAsync(AttachCommand(), CancellationToken.None);
        Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(null);
        return attached;
    }

    internal static TaskTrackerCard Card(
        string title = "Card title",
        string? version = "v1",
        string boardId = BoardId,
        string cardId = CardId) =>
        new(
            CardId: cardId,
            BoardId: boardId,
            ColumnId: "100",
            ColumnTitle: "In Progress",
            Title: title,
            Description: "body",
            UpdatedAt: Now,
            ColumnChangedAt: Now,
            Archived: false,
            RevisionTag: version);
}

internal sealed class FakeConnectionProvider : ITaskTrackerConnectionProvider
{
    public string TrackerKey => CardAttachmentServiceFixture.Tracker;

    public string DisplayName => "Kaiten";

    public Func<string, Task<TaskTrackerCard?>> OnGetCard { get; set; } =
        _ => Task.FromResult<TaskTrackerCard?>(null);

    public Task<TaskTrackerProbeResult> ProbeAsync(TaskTrackerConnectionDescriptor connection, CancellationToken ct) =>
        Task.FromResult(TaskTrackerProbeResult.Connected());

    public Task<IReadOnlyList<TaskTrackerSpaceTopology>> ListBoardsAsync(
        TaskTrackerConnectionDescriptor connection, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskTrackerSpaceTopology>>([]);

    public Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection, string boardId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);

    public Task<IReadOnlyList<TaskTrackerCard>> SearchCardsAsync(
        TaskTrackerConnectionDescriptor connection, string boardId, string? query, int limit, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);

    public Task<TaskTrackerCard?> GetCardAsync(
        TaskTrackerConnectionDescriptor connection, string cardId, CancellationToken ct) =>
        OnGetCard(cardId);

    public string? BuildCardWebUrl(TaskTrackerConnectionDescriptor connection, string cardId) =>
        $"{connection.BaseUrl.TrimEnd('/')}/{cardId}";
}

internal sealed class InMemoryCardAttachmentStore : IIntentCardAttachmentStore
{
    private readonly List<IntentCardAttachment> _items = [];

    public IReadOnlyList<IntentCardAttachment> Items => _items;

    public Task<IReadOnlyList<IntentCardAttachment>> ListByIntentAsync(IntentId intentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentCardAttachment>>(
            _items.Where(a => a.IntentId == intentId).OrderBy(a => a.CreatedAt).ToList());

    public Task<IntentCardAttachment?> GetAsync(CardAttachmentId id, CancellationToken ct) =>
        Task.FromResult<IntentCardAttachment?>(_items.FirstOrDefault(a => a.Id == id));

    public Task<IntentCardAttachment?> GetByCoordinateAsync(
        IntentId intentId, CardCoordinate coordinate, CancellationToken ct) =>
        Task.FromResult<IntentCardAttachment?>(_items.FirstOrDefault(a =>
            a.IntentId == intentId
            && a.Coordinate.Tracker == coordinate.Tracker
            && a.Coordinate.BoardId == coordinate.BoardId
            && a.Coordinate.CardId == coordinate.CardId));

    public Task<IntentCardAttachment> UpsertAsync(IntentCardAttachment attachment, CancellationToken ct)
    {
        _items.RemoveAll(a => a.Id == attachment.Id);
        _items.Add(attachment);
        return Task.FromResult(attachment);
    }

    public Task<bool> DeleteAsync(CardAttachmentId id, CancellationToken ct)
    {
        var removed = _items.RemoveAll(a => a.Id == id) > 0;
        return Task.FromResult(removed);
    }
}

internal sealed class PassthroughUnitOfWork : IUnitOfWork
{
    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

    public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
        work(ct);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
