using FluentAssertions;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Infrastructure.Tests.TaskTrackers.Sync;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerMirrorServiceTests(SqliteFixture sqlite)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static TaskTrackerCard Card(string title, string? description, string cardId = "card-42") =>
        new(cardId, "board-7", "col-1", "Todo", title, description, Now, Now, Archived: false);

    private static (TaskTrackerMirrorService Mirror, IIntentRepository Repo, ITaskTrackerCardLinkStore Links) Build(SqliteTestDatabase db)
    {
        var repo = db.GetRequiredService<IIntentRepository>();
        var ordering = db.GetRequiredService<IIntentOrderingRepository>();
        var links = db.GetRequiredService<ITaskTrackerCardLinkStore>();
        var uow = db.GetRequiredService<IUnitOfWork>();
        var mirror = new TaskTrackerMirrorService(repo, ordering, links, uow, new FixedTimeProvider(Now));
        return (mirror, repo, links);
    }

    [Fact(DisplayName = "existing == null создаёт draft-интент + CardSyncLink (Created, ContentChanged)")]
    public async Task Creates_intent_and_link_on_first_sight()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var (mirror, repo, links) = Build(db);

        var result = await mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing: null, CancellationToken.None);

        result.Created.Should().BeTrue();
        result.ContentChanged.Should().BeTrue();

        var intent = await repo.GetByIdAsync(new IntentId(result.IntentId), CancellationToken.None);
        intent.Should().NotBeNull();
        intent!.State.Status.Should().Be(IntentStatusNames.Draft);
        intent.State.Title.Should().Be("Title");
        intent.State.Text.Should().Be("Body");

        var link = await links.GetByCardAsync("kaiten", "board-7", "card-42", CancellationToken.None);
        link.Should().NotBeNull();
        link!.IntentId.Should().Be(result.IntentId);
        link.State.Should().Be(CardSyncLinkState.Linked);
    }

    [Fact(DisplayName = "Карта не изменилась относительно снапшота → ContentChanged=false, текст не трогается")]
    public async Task Unchanged_card_is_a_noop()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var (mirror, repo, links) = Build(db);
        var created = await mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing: null, CancellationToken.None);
        var existing = await links.GetByIntentAsync(created.IntentId, CancellationToken.None);

        var result = await mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing, CancellationToken.None);

        result.Created.Should().BeFalse();
        result.ContentChanged.Should().BeFalse();
        var intent = await repo.GetByIdAsync(new IntentId(created.IntentId), CancellationToken.None);
        intent!.State.Text.Should().Be("Body");
        intent.State.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "Карточка без описания → text = title, title заполнен")]
    public async Task Blank_description_seeds_text_from_title()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var (mirror, repo, _) = Build(db);

        var result = await mirror.ApplyAsync("kaiten", Card("Only title", null), existing: null, CancellationToken.None);

        var intent = await repo.GetByIdAsync(new IntentId(result.IntentId), CancellationToken.None);
        intent!.State.Title.Should().Be("Only title");
        intent.State.Text.Should().Be("Only title");
    }

    [Fact(DisplayName = "Меняется только заголовок карточки → title обновляется, current_version не растёт")]
    public async Task Title_only_change_does_not_bump_version()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var (mirror, repo, links) = Build(db);
        var created = await mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing: null, CancellationToken.None);
        var existing = await links.GetByIntentAsync(created.IntentId, CancellationToken.None);

        var result = await mirror.ApplyAsync("kaiten", Card("Renamed", "Body"), existing, CancellationToken.None);

        result.ContentChanged.Should().BeTrue();
        var intent = await repo.GetByIdAsync(new IntentId(created.IntentId), CancellationToken.None);
        intent!.State.Title.Should().Be("Renamed");
        intent.State.Text.Should().Be("Body");
        intent.State.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "Карта изменилась → текст интента обновляется, снапшот линка двигается, ContentChanged=true")]
    public async Task Changed_card_updates_intent_text()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var (mirror, repo, links) = Build(db);
        var created = await mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing: null, CancellationToken.None);
        var existing = await links.GetByIntentAsync(created.IntentId, CancellationToken.None);

        var result = await mirror.ApplyAsync("kaiten", Card("Renamed", "New body"), existing, CancellationToken.None);

        result.ContentChanged.Should().BeTrue();
        var intent = await repo.GetByIdAsync(new IntentId(created.IntentId), CancellationToken.None);
        intent!.State.Title.Should().Be("Renamed");
        intent.State.Text.Should().Be("New body");
        intent.State.CurrentVersion.Should().Be(2);

        var link = await links.GetByIntentAsync(created.IntentId, CancellationToken.None);
        link!.Snapshot.Title.Should().Be("Renamed");
        link.Snapshot.Description.Should().Be("New body");
    }

    [Fact(DisplayName = "Зеркальный интент удалён (линк есть, GetById null) → stale-линк сбрасывается и пересоздаётся")]
    public async Task Stale_link_is_dropped_and_recreated()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var (mirror, _, links) = Build(db);
        var stale = CardSyncLink.Create(
            "ghost-intent", new TaskTrackerCardLink("kaiten", "board-7", "card-42"),
            new CardSyncLinkSnapshot("Old", "Old", "col-1", "Todo"), CardSyncLinkCursors.Empty, Now);
        await links.SaveAsync(stale, CancellationToken.None);

        var result = await mirror.ApplyAsync("kaiten", Card("Title", "Body"), stale, CancellationToken.None);

        result.Created.Should().BeTrue();
        result.IntentId.Should().NotBe("ghost-intent");
        (await links.GetByIntentAsync("ghost-intent", CancellationToken.None)).Should().BeNull();
        var link = await links.GetByCardAsync("kaiten", "board-7", "card-42", CancellationToken.None);
        link!.IntentId.Should().Be(result.IntentId);
    }

    [Fact(DisplayName = "MarkStubAsync переводит линк в stub и идемпотентен")]
    public async Task MarkStub_persists_and_is_idempotent()
    {
        await using var db = await sqlite.CreateDatabaseAsync();
        var (mirror, _, links) = Build(db);
        var created = await mirror.ApplyAsync("kaiten", Card("Title", "Body"), existing: null, CancellationToken.None);
        var link = await links.GetByIntentAsync(created.IntentId, CancellationToken.None);

        await mirror.MarkStubAsync(link!, CancellationToken.None);
        await mirror.MarkStubAsync(link!, CancellationToken.None);

        var stubbed = await links.GetByIntentAsync(created.IntentId, CancellationToken.None);
        stubbed!.IsStub.Should().BeTrue();
    }
}
