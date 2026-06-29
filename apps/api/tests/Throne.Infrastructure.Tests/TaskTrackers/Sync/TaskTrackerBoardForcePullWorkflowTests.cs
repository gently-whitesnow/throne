using FluentAssertions;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers.Sync;

namespace Throne.Infrastructure.Tests.TaskTrackers.Sync;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerBoardForcePullWorkflowTests(SqliteFixture sqlite)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static TaskTrackerCard Card(string cardId, string title) =>
        new(cardId, "board-7", "col-1", "Todo", title, "Body", Now, Now, Archived: false);

    private sealed record Harness(
        TaskTrackerBoardForcePullWorkflow BoardForcePull,
        ITaskTrackerCardLinkStore Links,
        StubSyncProvider Provider);

    private static async Task<Harness> BuildAsync(SqliteTestDatabase db)
    {
        var repo = db.GetRequiredService<IIntentRepository>();
        var ordering = db.GetRequiredService<IIntentOrderingRepository>();
        var links = db.GetRequiredService<ITaskTrackerCardLinkStore>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();
        var linkRepo = db.GetRequiredService<IIntentLinkRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();
        var clock = new FixedTimeProvider(Now);
        await connections.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);

        var mirror = new TaskTrackerMirrorService(repo, ordering, links, uow, clock);
        var reconciler = new TaskTrackerChildLinkReconciler(linkRepo, uow, clock);
        var provider = new StubSyncProvider();
        var boardWorkflow = new TaskTrackerBoardSyncWorkflow(mirror, links);
        var boardForcePull = new TaskTrackerBoardForcePullWorkflow(
            [provider], connections, links, boardWorkflow, reconciler);
        return new Harness(boardForcePull, links, provider);
    }

    [Fact(DisplayName = "Доска с карточкой → Synced, зеркало создано (через detail-эскалацию)")]
    public async Task Board_with_card_syncs_and_mirrors()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = await BuildAsync(db);
        h.Provider.ListBoardCards = _ => [Card("card-1", "First")];
        h.Provider.GetCard = _ => Card("card-1", "First");

        var result = await h.BoardForcePull.ForcePullAsync("kaiten", "board-7", CancellationToken.None);

        result.Status.Should().Be(BoardForcePullStatus.Synced);
        result.CardsChanged.Should().Be(1);
        var link = await h.Links.GetByCardAsync("kaiten", "board-7", "card-1", CancellationToken.None);
        link.Should().NotBeNull();
    }

    [Fact(DisplayName = "Нет соединения для трекера → NotConnected")]
    public async Task Unknown_tracker_is_not_connected()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = await BuildAsync(db);

        var result = await h.BoardForcePull.ForcePullAsync("jira", "board-7", CancellationToken.None);

        result.Status.Should().Be(BoardForcePullStatus.NotConnected);
    }

    [Fact(DisplayName = "Провайдер упал на листинге → Unavailable")]
    public async Task Provider_failure_is_unavailable()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = await BuildAsync(db);
        h.Provider.ListBoardCards = _ => throw new InvalidOperationException("boom");

        var result = await h.BoardForcePull.ForcePullAsync("kaiten", "board-7", CancellationToken.None);

        result.Status.Should().Be(BoardForcePullStatus.Unavailable);
    }
}
