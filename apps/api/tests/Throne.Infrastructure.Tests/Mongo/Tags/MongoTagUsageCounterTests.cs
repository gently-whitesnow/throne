using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Tags;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.Mongo.Tags;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoTagUsageCounterTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Base = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "usage_count двигается консистентно при create / set-tags / delete интентов")]
    public async Task Usage_count_stays_consistent_across_write_path()
    {
        var ctx = await NewContextAsync();
        var a = await CreateTagAsync(ctx, "alpha");
        var b = await CreateTagAsync(ctx, "beta");
        var c = await CreateTagAsync(ctx, "gamma");

        var intent1 = await SeedIntentAsync(ctx, "first", [a, b]);
        await SeedIntentAsync(ctx, "second", [a]);

        (await UsageAsync(ctx, a)).Should().Be(2);
        (await UsageAsync(ctx, b)).Should().Be(1);
        (await UsageAsync(ctx, c)).Should().Be(0);

        // Replace [a,b] with [b,c]: a drops, c gains, b unchanged.
        var outcome = await ctx.Uow.ExecuteAsync(
            ct => ctx.Intents.SetTagsAsync(intent1.Id, intent1.State.CurrentVersion, [b, c], Base.AddMinutes(1), ct),
            CancellationToken.None);
        outcome.Should().BeOfType<SetIntentTagsOutcome.Updated>();

        (await UsageAsync(ctx, a)).Should().Be(1);
        (await UsageAsync(ctx, b)).Should().Be(1);
        (await UsageAsync(ctx, c)).Should().Be(1);

        // Delete intent2 (carries only a): a drops to 0.
        var second = await SeedIntentAsync(ctx, "third", [a]);
        (await UsageAsync(ctx, a)).Should().Be(2);
        await ctx.Uow.ExecuteAsync(ct => ctx.Intents.DeleteAsync(second.Id, ct), CancellationToken.None);
        (await UsageAsync(ctx, a)).Should().Be(1);
    }

    [Fact(DisplayName = "ListPageAsync сортирует по usage desc, ищет по подстроке и пагинирует курсором")]
    public async Task ListPage_orders_by_usage_desc_with_search_and_cursor()
    {
        var ctx = await NewContextAsync();
        var hot = await CreateTagAsync(ctx, "hot-topic");
        var warm = await CreateTagAsync(ctx, "warm-topic");
        var cold = await CreateTagAsync(ctx, "cold");

        await SeedIntentAsync(ctx, "i1", [hot, warm]);
        await SeedIntentAsync(ctx, "i2", [hot, warm]);
        await SeedIntentAsync(ctx, "i3", [hot]);
        // usage: hot=3, warm=2, cold=0

        var page = await ctx.Tags.ListPageAsync(new TagListSpec(Search: null, Limit: 50, Cursor: null), CancellationToken.None);
        page.Items.Select(i => i.Tag.Id.Value).Should().Equal(hot.Value, warm.Value, cold.Value);
        page.Items.Select(i => i.IntentsCount).Should().Equal(3, 2, 0);
        page.NextCursor.Should().BeNull();

        var searched = await ctx.Tags.ListPageAsync(new TagListSpec(Search: "topic", Limit: 50, Cursor: null), CancellationToken.None);
        searched.Items.Select(i => i.Tag.Id.Value).Should().Equal(hot.Value, warm.Value);

        var first = await ctx.Tags.ListPageAsync(new TagListSpec(Search: null, Limit: 2, Cursor: null), CancellationToken.None);
        first.Items.Select(i => i.Tag.Id.Value).Should().Equal(hot.Value, warm.Value);
        first.NextCursor.Should().NotBeNull();

        var second = await ctx.Tags.ListPageAsync(new TagListSpec(Search: null, Limit: 2, Cursor: first.NextCursor), CancellationToken.None);
        second.Items.Select(i => i.Tag.Id.Value).Should().Equal(cold.Value);
        second.NextCursor.Should().BeNull();
    }

    private static async Task<int> UsageAsync(TestContext ctx, TagId id)
    {
        var doc = await ctx.Database
            .GetCollection<TagDocument>(MongoCollectionNames.Tags)
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync();
        return doc!.UsageCount;
    }

    private static async Task<TagId> CreateTagAsync(TestContext ctx, string name)
    {
        var created = await ctx.Uow.ExecuteAsync(ct => ctx.Tags.CreateAsync(name, Base, ct), CancellationToken.None);
        return ((CreateTagOutcome.Created)created).Tag.Id;
    }

    private static async Task<Intent> SeedIntentAsync(TestContext ctx, string text, IReadOnlyList<TagId> tagIds)
    {
        var id = IntentId.New();
        var intent = Intent.Create(id, text, tagIds, Base);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, text, Base, TextVersionAuthor.Agent);
        var statusChange = IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"), id, intent.State.CurrentVersion,
            intent.State.Status, intent.State.Status, "test:create", Base, IntentTrainingAuthor.Agent);
        await ctx.Uow.ExecuteAsync(
            ct => ctx.Intents.CreateAsync(intent, version, statusChange, Array.Empty<Tag>(), ct),
            CancellationToken.None);
        return intent;
    }

    private async Task<TestContext> NewContextAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var intents = new MongoIntentRepository(db, sessions, new MongoIntentEventRepository(db, sessions));
        var tags = new MongoTagRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return new TestContext(db, intents, tags, uow);
    }

    private sealed record TestContext(
        IMongoDatabase Database,
        MongoIntentRepository Intents,
        MongoTagRepository Tags,
        IUnitOfWork Uow);
}
