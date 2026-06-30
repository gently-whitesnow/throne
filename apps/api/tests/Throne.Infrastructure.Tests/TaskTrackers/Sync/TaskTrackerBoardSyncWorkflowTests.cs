using FluentAssertions;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.Intents;

namespace Throne.Infrastructure.Tests.TaskTrackers.Sync;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerBoardSyncWorkflowTests(SqliteFixture sqlite)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TaskTrackerConnectionDescriptor Descriptor = new("https://acme.kaiten.ru", "tok");

    private sealed record Harness(
        TaskTrackerBoardSyncWorkflow Workflow,
        TaskTrackerMirrorService Mirror,
        ITaskTrackerCardLinkStore Links,
        IIntentRepository Repo,
        StubSyncProvider Provider);

    private static Harness Build(SqliteTestDatabase db)
    {
        var repo = db.GetRequiredService<IIntentRepository>();
        var ordering = db.GetRequiredService<IIntentOrderingRepository>();
        var links = db.GetRequiredService<ITaskTrackerCardLinkStore>();
        var uow = db.GetRequiredService<IUnitOfWork>();
        var clock = new FixedTimeProvider(Now);
        var mirror = new TaskTrackerMirrorService(repo, ordering, links, uow, clock);
        var provider = new StubSyncProvider();
        var workflow = new TaskTrackerBoardSyncWorkflow(mirror, links);
        return new Harness(workflow, mirror, links, repo, provider);
    }

    private static TaskTrackerCard ListRow(string cardId, string title, string? revisionTag) =>
        new(cardId, "board-7", "col-1", "Todo", title, Description: null, Now, Now, Archived: false, RevisionTag: revisionTag);

    private static TaskTrackerCard Detail(string cardId, string title, string description, string? revisionTag) =>
        new(cardId, "board-7", "col-1", "Todo", title, description, Now, Now, Archived: false, RevisionTag: revisionTag);

    [Fact(DisplayName = "Новая карточка (нет линка) → detail-fetch, description пуллится из detail, не list")]
    public async Task First_sight_card_escalates_to_detail_and_pulls_description()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        var detailCalls = new List<string>();
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", "v1")];
        h.Provider.GetCard = id => { detailCalls.Add(id); return Detail("card-1", "Title", "# Body\nmarkdown", "v1"); };

        var outcome = await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        outcome.Failed.Should().BeFalse();
        outcome.ChangedCardIds.Should().Equal("card-1");
        detailCalls.Should().Equal("card-1");

        var link = await h.Links.GetByCardAsync("kaiten", "board-7", "card-1", CancellationToken.None);
        link.Should().NotBeNull();
        link!.Cursors.RevisionTag.Should().Be("v1");
        var intent = await h.Repo.GetByIdAsync(new IntentId(link.IntentId), CancellationToken.None);
        intent!.State.Text.Should().Be("# Body\nmarkdown");
    }

    [Fact(DisplayName = "Ревизия не изменилась → detail НЕ запрашивается, intent остаётся прежним")]
    public async Task Unchanged_revision_skips_detail_fetch()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", "v1")];
        h.Provider.GetCard = _ => Detail("card-1", "Title", "# Body", "v1");
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);
        var detailCalls = new List<string>();
        h.Provider.GetCard = _ => { detailCalls.Add("called"); throw new InvalidOperationException("detail must not be requested when revision is unchanged"); };

        var outcome = await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        outcome.Failed.Should().BeFalse();
        outcome.ChangedCardIds.Should().BeEmpty();
        detailCalls.Should().BeEmpty();
    }

    [Fact(DisplayName = "Ревизия выросла → detail-fetch, intent.text обновляется новым описанием")]
    public async Task Bumped_revision_refetches_detail_and_updates_intent_text()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", "v1")];
        h.Provider.GetCard = _ => Detail("card-1", "Title", "first", "v1");
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        var detailCalls = new List<string>();
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", "v2")];
        h.Provider.GetCard = id => { detailCalls.Add(id); return Detail("card-1", "Title", "second", "v2"); };
        var outcome = await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        outcome.ChangedCardIds.Should().Equal("card-1");
        detailCalls.Should().Equal("card-1");
        var link = await h.Links.GetByCardAsync("kaiten", "board-7", "card-1", CancellationToken.None);
        link!.Cursors.RevisionTag.Should().Be("v2");
        var intent = await h.Repo.GetByIdAsync(new IntentId(link.IntentId), CancellationToken.None);
        intent!.State.Text.Should().Be("second");
    }

    [Fact(DisplayName = "Provider не отдаёт RevisionTag (null) → detail-fetch каждый раз, degrade-gracefully")]
    public async Task Missing_revision_tag_forces_detail_fetch_every_time()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", revisionTag: null)];
        h.Provider.GetCard = _ => Detail("card-1", "Title", "body", revisionTag: null);
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        var detailCalls = new List<string>();
        h.Provider.GetCard = id => { detailCalls.Add(id); return Detail("card-1", "Title", "body", revisionTag: null); };
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        detailCalls.Should().Equal("card-1");
    }

    [Fact(DisplayName = "Detail упал → итерация по карточке пропущена, board sync не валится")]
    public async Task Detail_failure_skips_card_without_failing_board_sync()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", "v1")];
        h.Provider.GetCard = _ => throw new InvalidOperationException("detail boom");

        var outcome = await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        outcome.Failed.Should().BeFalse();
        outcome.ChangedCardIds.Should().BeEmpty();
        (await h.Links.GetByCardAsync("kaiten", "board-7", "card-1", CancellationToken.None)).Should().BeNull();
    }

    [Fact(DisplayName = "List упал → BoardSyncOutcome.Failed=true, никаких detail-вызовов")]
    public async Task List_failure_propagates_failed_outcome()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        var detailCalls = new List<string>();
        h.Provider.ListBoardCards = _ => throw new InvalidOperationException("list boom");
        h.Provider.GetCard = id => { detailCalls.Add(id); return null; };

        var outcome = await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        outcome.Failed.Should().BeTrue();
        outcome.ChangedCardIds.Should().BeEmpty();
        detailCalls.Should().BeEmpty();
    }

    [Fact(DisplayName = "Карточка ушла с доски → линк помечается stub")]
    public async Task Vanished_card_is_stubbed()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", "v1")];
        h.Provider.GetCard = _ => Detail("card-1", "Title", "body", "v1");
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        h.Provider.ListBoardCards = _ => [];
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        var link = await h.Links.GetByCardAsync("kaiten", "board-7", "card-1", CancellationToken.None);
        link!.IsStub.Should().BeTrue();
    }

    [Fact(DisplayName = "Карточка ушла с доски → mirror-intent уходит в reject")]
    public async Task Vanished_card_rejects_mirror_intent()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var h = Build(db);
        h.Provider.ListBoardCards = _ => [ListRow("card-1", "Title", "v1")];
        h.Provider.GetCard = _ => Detail("card-1", "Title", "body", "v1");
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);
        var link = await h.Links.GetByCardAsync("kaiten", "board-7", "card-1", CancellationToken.None);

        h.Provider.ListBoardCards = _ => [];
        await h.Workflow.SyncBoardAsync(h.Provider, Descriptor, "board-7", CancellationToken.None);

        var intent = await h.Repo.GetByIdAsync(new IntentId(link!.IntentId), CancellationToken.None);
        intent!.State.Status.Should().Be(IntentStatusNames.Reject);
    }
}
