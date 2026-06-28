using FluentAssertions;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.TaskTrackers.Sync;

/// <summary>
/// Board-linked mirror intents must surface as their own rail group (counts + list filter) and stay
/// out of the native active untagged bucket, so a board's cards group by board rather than «Без тегов».
/// </summary>
[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerBoardRailGroupingTests(SqliteFixture sqlite)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static TaskTrackerCard Card(string cardId, string title) =>
        new(cardId, "board-7", "col-1", "Todo", title, "Body", Now, Now, Archived: false);

    [Fact(DisplayName = "Мирор доски считается в boards и исключён из untagged")]
    public async Task Board_mirror_counts_in_boards_not_untagged()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var ordering = db.GetRequiredService<IIntentOrderingRepository>();
        var links = db.GetRequiredService<ITaskTrackerCardLinkStore>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();
        var uow = db.GetRequiredService<IUnitOfWork>();
        var clock = new FixedTimeProvider(Now);
        var mirror = new TaskTrackerMirrorService(repo, ordering, links, uow, clock);

        await connections.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);
        await connections.SaveSelectionAsync(
            "kaiten",
            [new TaskTrackerBoardSelection("space-1", "Space", "board-7", "Board Seven", "none")],
            CancellationToken.None);

        var mirrored = await mirror.ApplyAsync("kaiten", Card("card-1", "Card One"), existing: null, CancellationToken.None);
        var plain = await SeedUntaggedAsync(repo, uow);

        var counts = await repo.GetContextCountsAsync([], CancellationToken.None);

        counts.Untagged.Should().Be(1, "the plain intent stays untagged, the board mirror is diverted");
        counts.Boards.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new IntentBoardCount("kaiten", "board-7", "Board Seven", 1));

        var page = await repo.ListPagedAsync(
            new IntentListSpec(
                Statuses: ["draft", "interview", "ready_for_work", "work", "awaiting_operator"],
                TagId: null, Untagged: false, Pinned: false, Query: null,
                Sort: IntentListSort.UpdatedDesc, Limit: 50, Cursor: null, BoardId: "board-7"),
            CancellationToken.None);

        page.Items.Select(i => i.Id.Value).Should().Equal(mirrored.IntentId);
        page.Items.Select(i => i.Id.Value).Should().NotContain(plain.Id.Value);
    }

    [Fact(DisplayName = "Подключённая доска без карточек — пустая группа со счётчиком 0")]
    public async Task Connected_board_without_cards_is_empty_group()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var connections = db.GetRequiredService<ITaskTrackerConnectionStore>();

        await connections.SaveConnectionAsync("kaiten", "https://acme.kaiten.ru", "tok", CancellationToken.None);
        await connections.SaveSelectionAsync(
            "kaiten",
            [new TaskTrackerBoardSelection("space-1", "Space", "board-7", "Board Seven", "none")],
            CancellationToken.None);

        var counts = await repo.GetContextCountsAsync([], CancellationToken.None);

        counts.Boards.Should().ContainSingle()
            .Which.Count.Should().Be(0);
    }

    private static async Task<Intent> SeedUntaggedAsync(IIntentRepository repo, IUnitOfWork uow)
    {
        var id = IntentId.New();
        var intent = Intent.Create(id, "plain untagged", Array.Empty<TagId>(), Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "plain untagged", Now, TextVersionAuthor.Agent);
        var statusChange = IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"), intent.Id, intent.State.CurrentVersion,
            intent.State.Status, intent.State.Status, "test:create", Now, IntentTrainingAuthor.Agent);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, statusChange, Array.Empty<Tag>(), ct),
            CancellationToken.None);
        return intent;
    }
}
