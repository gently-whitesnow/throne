using FluentAssertions;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Search;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.EfCore.Persistence;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfIntentSearchTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Base = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Ранжированный поиск: более релевантный (короткий, с термином) интент идёт первым")]
    public async Task Ranks_more_relevant_first()
    {
        var db = await fixture.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        var focused = await Seed(repo, uow, "договор поставки", Base);
        var diluted = await Seed(
            repo, uow,
            "длинный текст про разные темы где слово договор встречается единожды среди прочего",
            Base.AddMinutes(1));

        var page = await repo.ListPagedAsync(QuerySpec("договор"), CancellationToken.None);

        page.Items.Select(i => i.Id.Value).Should().Equal(focused.Id.Value, diluted.Id.Value);
        page.NextCursor.Should().BeNull();
    }

    [Fact(DisplayName = "Префиксный поиск: незаконченный токен находит интент")]
    public async Task Prefix_matches()
    {
        var db = await fixture.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        var hit = await Seed(repo, uow, "автокомплит связей интентов", Base);
        await Seed(repo, uow, "совсем другое", Base.AddMinutes(1));

        var page = await repo.ListPagedAsync(QuerySpec("автоком"), CancellationToken.None);

        page.Items.Select(i => i.Id.Value).Should().Equal(hit.Id.Value);
    }

    [Fact(DisplayName = "Сниппет: совпавший термин обёрнут маркерами подсветки")]
    public async Task Snippet_highlights_match()
    {
        var db = await fixture.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        var hit = await Seed(repo, uow, "ранжированный поиск интентов", Base);

        var page = await repo.ListPagedAsync(QuerySpec("поиск"), CancellationToken.None);

        page.Snippets.Should().NotBeNull();
        var snippet = page.Snippets![hit.Id.Value];
        snippet.Should().Contain(IntentSearchMarkers.Open + "поиск" + IntentSearchMarkers.Close);
    }

    [Fact(DisplayName = "Синхронизация индекса: правка текста переиндексирует интент через триггер")]
    public async Task Index_follows_text_edits()
    {
        var db = await fixture.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        var intent = await Seed(repo, uow, "уникальныйтермин один", Base);

        await uow.ExecuteAsync(
            ct => repo.ReplaceTextAsync(
                intent.Id, intent.State.CurrentVersion, "уникальныйтермин", "заменённыйтермин",
                TextVersionAuthor.Agent, Base.AddMinutes(1), ct),
            CancellationToken.None);

        var gone = await repo.ListPagedAsync(QuerySpec("уникальныйтермин"), CancellationToken.None);
        gone.Items.Should().BeEmpty();

        var found = await repo.ListPagedAsync(QuerySpec("заменённыйтермин"), CancellationToken.None);
        found.Items.Select(i => i.Id.Value).Should().Equal(intent.Id.Value);
    }

    [Fact(DisplayName = "Синхронизация индекса: удаление интента убирает его из выдачи")]
    public async Task Index_follows_delete()
    {
        var db = await fixture.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();

        var intent = await Seed(repo, uow, "удаляемыйтермин здесь", Base);
        await uow.ExecuteAsync(ct => repo.DeleteAsync(intent.Id, ct), CancellationToken.None);

        var page = await repo.ListPagedAsync(QuerySpec("удаляемыйтермин"), CancellationToken.None);
        page.Items.Should().BeEmpty();
    }

    [Fact(DisplayName = "Поиск без осмысленных токенов возвращает пустую страницу, а не ошибку")]
    public async Task Punctuation_only_query_is_empty()
    {
        var db = await fixture.CreateDatabaseAsync();
        var repo = db.GetRequiredService<IIntentRepository>();
        var uow = db.GetRequiredService<IUnitOfWork>();
        await Seed(repo, uow, "любой текст", Base);

        var page = await repo.ListPagedAsync(QuerySpec("--- ()"), CancellationToken.None);
        page.Items.Should().BeEmpty();
    }

    private static IntentListSpec QuerySpec(string query) => new(
        Statuses: null, TagId: null, Untagged: false, Pinned: false,
        Query: query, Sort: IntentListSort.UpdatedDesc, Limit: 20, Cursor: null);

    private static async Task<Intent> Seed(
        IIntentRepository repo, IUnitOfWork uow, string text, DateTimeOffset at)
    {
        var id = IntentId.New();
        var intent = Intent.Create(id, text, [], at);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            text, at, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent, at), Array.Empty<Tag>(), ct),
            CancellationToken.None);
        return intent;
    }

    private static IntentStatusChange InitialStatusChange(Intent intent, DateTimeOffset at) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"), intent.Id, intent.State.CurrentVersion,
            intent.State.Status, intent.State.Status, "test:create", at, IntentTrainingAuthor.Agent);
}
