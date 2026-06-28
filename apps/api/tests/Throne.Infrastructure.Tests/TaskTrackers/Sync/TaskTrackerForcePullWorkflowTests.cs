using FluentAssertions;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Infrastructure.Tests.TaskTrackers.Sync;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerForcePullWorkflowTests(SqliteFixture sqlite)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static TaskTrackerCard Card(string title, string? description) =>
        new("card-42", "board-7", "col-1", "Todo", title, description, Now, Now, Archived: false);

    private sealed record Harness(
        TaskTrackerForcePullWorkflow ForcePull,
        TaskTrackerMirrorService Mirror,
        ITaskTrackerCardLinkStore Links,
        IIntentRepository Repo,
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
        var forcePull = new TaskTrackerForcePullWorkflow(links, connections, [provider], mirror, reconciler);
        return new Harness(forcePull, mirror, links, repo, provider);
    }

    [Fact(DisplayName = "Интент без линка → NotLinked")]
    public async Task No_link_is_not_linked()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = await BuildAsync(db);

        var result = await h.ForcePull.ForcePullAsync("unknown-intent", CancellationToken.None);

        result.Status.Should().Be(ForcePullStatus.NotLinked);
    }

    [Fact(DisplayName = "GetCardAsync вернул null → Stubbed, линк помечен stub")]
    public async Task Missing_card_stubs_the_link()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = await BuildAsync(db);
        var created = await h.Mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing: null, CancellationToken.None);
        h.Provider.GetCard = _ => null;

        var result = await h.ForcePull.ForcePullAsync(created.IntentId, CancellationToken.None);

        result.Status.Should().Be(ForcePullStatus.Stubbed);
        result.State.Should().Be(CardSyncLinkState.Stub);
        var link = await h.Links.GetByIntentAsync(created.IntentId, CancellationToken.None);
        link!.IsStub.Should().BeTrue();
    }

    [Fact(DisplayName = "GetCardAsync вернул карту → Refreshed, текст интента обновлён")]
    public async Task Returned_card_refreshes_the_intent()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = await BuildAsync(db);
        var created = await h.Mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing: null, CancellationToken.None);
        h.Provider.GetCard = _ => Card("Renamed", "New body");

        var result = await h.ForcePull.ForcePullAsync(created.IntentId, CancellationToken.None);

        result.Status.Should().Be(ForcePullStatus.Refreshed);
        result.State.Should().Be(CardSyncLinkState.Linked);
        var intent = await h.Repo.GetByIdAsync(new IntentId(created.IntentId), CancellationToken.None);
        intent!.State.Title.Should().Be("Renamed");
        intent.State.Text.Should().Be("New body");
    }
}
