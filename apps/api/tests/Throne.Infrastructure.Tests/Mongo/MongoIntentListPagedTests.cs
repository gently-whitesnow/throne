using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentListPagedTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Base = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ListPagedAsync сортирует по updated_desc и возвращает next_cursor для пагинации")]
    public async Task Pages_by_updated_desc_with_cursor()
    {
        var (repo, uow) = await NewScopeAsync();

        var a = await Seed(repo, uow, "alpha", Base);
        var b = await Seed(repo, uow, "beta", Base.AddMinutes(1));
        var c = await Seed(repo, uow, "gamma", Base.AddMinutes(2));

        var first = await repo.ListPagedAsync(
            new IntentListSpec(Statuses: null, TagId: null, Query: null, Sort: IntentListSort.UpdatedDesc, Limit: 2, Cursor: null),
            CancellationToken.None);

        first.Items.Select(i => i.Id.Value).Should().Equal(c.Id.Value, b.Id.Value);
        first.NextCursor.Should().NotBeNull();

        var second = await repo.ListPagedAsync(
            new IntentListSpec(Statuses: null, TagId: null, Query: null, Sort: IntentListSort.UpdatedDesc, Limit: 2, Cursor: first.NextCursor),
            CancellationToken.None);

        second.Items.Select(i => i.Id.Value).Should().Equal(a.Id.Value);
        second.NextCursor.Should().BeNull();
    }

    [Fact(DisplayName = "ListPagedAsync фильтрует по статусу и тегу")]
    public async Task Filters_by_status_and_tag()
    {
        var (repo, uow) = await NewScopeAsync();
        var sharedTag = TagId.New();
        var otherTag = TagId.New();

        var draft = await Seed(repo, uow, "draft text", Base, [sharedTag]);
        var doneIntent = await Seed(repo, uow, "done text", Base.AddMinutes(1), [sharedTag]);
        await uow.ExecuteAsync(
            ct => repo.SetStatusAsync(doneIntent.Id, IntentStatusNames.Done, null, IntentTrainingAuthor.System, "test", Base.AddMinutes(2), ct),
            CancellationToken.None);
        await Seed(repo, uow, "untagged-other", Base.AddMinutes(3), [otherTag]);

        var byTag = await repo.ListPagedAsync(
            new IntentListSpec(Statuses: null, TagId: sharedTag, Query: null, Sort: IntentListSort.CreatedAsc, Limit: 50, Cursor: null),
            CancellationToken.None);
        byTag.Items.Select(i => i.Id.Value).Should().BeEquivalentTo(new[] { draft.Id.Value, doneIntent.Id.Value });

        var byStatus = await repo.ListPagedAsync(
            new IntentListSpec(Statuses: ["done"], TagId: null, Query: null, Sort: IntentListSort.UpdatedDesc, Limit: 50, Cursor: null),
            CancellationToken.None);
        byStatus.Items.Should().HaveCount(1);
        byStatus.Items[0].Id.Value.Should().Be(doneIntent.Id.Value);
    }

    [Fact(DisplayName = "ListPagedAsync ищет case-insensitive по подстроке Intent.text")]
    public async Task Filters_by_query_substring()
    {
        var (repo, uow) = await NewScopeAsync();
        var match = await Seed(repo, uow, "Refactor MCP TOOLS", Base);
        await Seed(repo, uow, "unrelated", Base.AddMinutes(1));

        var page = await repo.ListPagedAsync(
            new IntentListSpec(Statuses: null, TagId: null, Query: "mcp tools", Sort: IntentListSort.UpdatedDesc, Limit: 50, Cursor: null),
            CancellationToken.None);

        page.Items.Should().HaveCount(1);
        page.Items[0].Id.Value.Should().Be(match.Id.Value);
    }

    private static async Task<Intent> Seed(
        MongoIntentRepository repo,
        IUnitOfWork uow,
        string text,
        DateTimeOffset at,
        IReadOnlyList<TagId>? tagIds = null)
    {
        var id = IntentId.New();
        var intent = Intent.Create(id, "user-1", text, tagIds ?? [TagId.New()], at);
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
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.CurrentVersion,
            intent.Status,
            intent.Status,
            "test:create",
            at,
            IntentTrainingAuthor.Agent);

    private async Task<(MongoIntentRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor(), new MongoIntentEventRepository(db, sessions));
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return (repo, uow);
    }
}
