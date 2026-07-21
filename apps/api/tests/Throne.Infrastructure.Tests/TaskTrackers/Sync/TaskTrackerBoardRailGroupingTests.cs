using FluentAssertions;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TaskTrackers;
using Throne.Domain.TextVersions;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.TaskTrackers.Sync;

/// <summary>
/// Read-path board facet (ADR-0052): a board's attached cards surface as their own rail group
/// (distinct active intents with an attachment) and drive the board list filter. Attaching a card is
/// read-only context, so the intent ALSO stays in its native tag/untagged bucket — no divert.
/// </summary>
[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerBoardRailGroupingTests(SqliteFixture sqlite)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Приложенная карточка считается в boards и НЕ уводит интент из untagged")]
    public async Task Board_attachment_counts_in_boards_and_stays_in_untagged()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var attachments = db.GetRequiredService<IIntentCardAttachmentStore>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        await SeedBoardSelectionAsync(connections);

        var attached = await SeedIntentAsync(repo, uow, tagIds: []);
        await AttachAsync(attachments, uow, attached.Id.Value, cardId: "card-1");
        var plain = await SeedIntentAsync(repo, uow, tagIds: []);

        var counts = await repo.GetContextCountsAsync([], CancellationToken.None);

        counts.Untagged.Should().Be(2, "the attached intent is read-only context and stays untagged too");
        counts.Boards.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new IntentBoardCount("kaiten", "board-7", "Board Seven", 1));

        var page = await ListBoardAsync(repo, "kaiten", "board-7");
        page.Items.Select(i => i.Id.Value).Should().Equal(attached.Id.Value);
        page.Items.Select(i => i.Id.Value).Should().NotContain(plain.Id.Value);
    }

    [Fact(DisplayName = "Тегированный интент с карточкой остаётся в теге И появляется в board-группе")]
    public async Task Tagged_intent_with_attachment_stays_in_tag_and_appears_in_board()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var attachments = db.GetRequiredService<IIntentCardAttachmentStore>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        await SeedBoardSelectionAsync(connections);

        var tag = TagId.New();
        var intent = await SeedIntentAsync(repo, uow, tagIds: [tag]);
        await AttachAsync(attachments, uow, intent.Id.Value, cardId: "card-1");

        var counts = await repo.GetContextCountsAsync([], CancellationToken.None);

        counts.Untagged.Should().Be(0);
        counts.Tags.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new IntentTagCount(tag.Value, 1));
        counts.Boards.Should().ContainSingle().Which.Count.Should().Be(1);
    }

    [Fact(DisplayName = "Несколько карточек одного интента на доске считаются как один distinct-интент")]
    public async Task Multiple_attachments_same_board_count_distinct_intent_once()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var attachments = db.GetRequiredService<IIntentCardAttachmentStore>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        await SeedBoardSelectionAsync(connections);

        var intent = await SeedIntentAsync(repo, uow, tagIds: []);
        await AttachAsync(attachments, uow, intent.Id.Value, cardId: "card-1");
        await AttachAsync(attachments, uow, intent.Id.Value, cardId: "card-2");

        var counts = await repo.GetContextCountsAsync([], CancellationToken.None);

        counts.Boards.Should().ContainSingle().Which.Count.Should().Be(1);

        var page = await ListBoardAsync(repo, "kaiten", "board-7");
        page.Items.Select(i => i.Id.Value).Should().Equal(intent.Id.Value);
    }

    [Fact(DisplayName = "Board filter scopes by tracker and board_id, not board_id alone")]
    public async Task Board_filter_scopes_by_tracker_and_board_id()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var attachments = db.GetRequiredService<IIntentCardAttachmentStore>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        await SeedBoardSelectionAsync(connections, tracker: "kaiten", title: "Kaiten Board");
        await SeedBoardSelectionAsync(connections, tracker: "jira", title: "Jira Board");

        var kaitenIntent = await SeedIntentAsync(repo, uow, tagIds: []);
        var jiraIntent = await SeedIntentAsync(repo, uow, tagIds: []);
        await AttachAsync(attachments, uow, kaitenIntent.Id.Value, cardId: "kaiten-card", tracker: "kaiten");
        await AttachAsync(attachments, uow, jiraIntent.Id.Value, cardId: "jira-card", tracker: "jira");

        var counts = await repo.GetContextCountsAsync([], CancellationToken.None);
        counts.Boards.Should().Contain(b => b.Tracker == "kaiten" && b.BoardId == "board-7" && b.Count == 1);
        counts.Boards.Should().Contain(b => b.Tracker == "jira" && b.BoardId == "board-7" && b.Count == 1);

        var kaitenPage = await ListBoardAsync(repo, "kaiten", "board-7");
        var jiraPage = await ListBoardAsync(repo, "jira", "board-7");

        kaitenPage.Items.Select(i => i.Id.Value).Should().Equal(kaitenIntent.Id.Value);
        jiraPage.Items.Select(i => i.Id.Value).Should().Equal(jiraIntent.Id.Value);
    }

    [Fact(DisplayName = "Подключённая доска без карточек — пустая группа со счётчиком 0")]
    public async Task Connected_board_without_cards_is_empty_group()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();

        await SeedBoardSelectionAsync(connections);

        var counts = await repo.GetContextCountsAsync([], CancellationToken.None);

        counts.Boards.Should().ContainSingle().Which.Count.Should().Be(0);
    }

    private static async Task SeedBoardSelectionAsync(
        ITaskTrackerConnectionStore connections,
        string tracker = "kaiten",
        string title = "Board Seven")
    {
        await connections.SaveConnectionAsync(tracker, "https://acme.example", "tok", CancellationToken.None);
        await connections.SaveSelectionAsync(
            tracker,
            [new TaskTrackerBoardSelection("space-1", "Space", "board-7", title, "none")],
            CancellationToken.None);
    }

    private static Task AttachAsync(
        IIntentCardAttachmentStore attachments,
        IUnitOfWork uow,
        string intentId,
        string cardId,
        string tracker = "kaiten")
    {
        var attachment = IntentCardAttachment.Create(
            id: CardAttachmentId.New(),
            intentId: new IntentId(intentId),
            coordinate: new CardCoordinate(tracker, "board-7", cardId),
            snapshot: new CardSnapshot(
                Text: "Card " + cardId + "\n\nbody",
                ColumnTitle: "Todo",
                Archived: false,
                CardVersion: "v1",
                FetchedAt: Now),
            now: Now);
        return uow.ExecuteAsync(ct => attachments.UpsertAsync(attachment, ct), CancellationToken.None);
    }

    private static async Task<Intent> SeedIntentAsync(
        IIntentRepository repo, IUnitOfWork uow, IReadOnlyList<TagId> tagIds)
    {
        var id = IntentId.New();
        var intent = Intent.Create(id, "intent text", tagIds, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "intent text", Now, TextVersionAuthor.Agent);
        var statusChange = IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"), intent.Id, intent.State.CurrentVersion,
            intent.State.Status, intent.State.Status, "test:create", Now, IntentTrainingAuthor.Agent);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, statusChange, Array.Empty<Tag>(), ct),
            CancellationToken.None);
        return intent;
    }

    private static Task<IntentListPage> ListBoardAsync(IIntentRepository repo, string tracker, string boardId) =>
        repo.ListPagedAsync(
            new IntentListSpec(
                Statuses: ["draft", "interview", "ready_for_work", "work", "awaiting_operator"],
                TagId: null, Untagged: false, Pinned: false, Query: null,
                Sort: IntentListSort.UpdatedDesc, Limit: 50, Cursor: null,
                BoardTracker: tracker, BoardId: boardId),
            CancellationToken.None);
}
