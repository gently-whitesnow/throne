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
public class MongoTagLastAttachedAtTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Base = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "last_attached_at двигается только при привязке тега к интенту")]
    public async Task Last_attached_at_updates_only_on_attach()
    {
        var ctx = await NewContextAsync();
        var a = await CreateTagAsync(ctx, "alpha");
        var b = await CreateTagAsync(ctx, "beta");
        var c = await CreateTagAsync(ctx, "gamma");

        var intent1 = await SeedIntentAsync(ctx, "first", [a, b]);
        await SeedIntentAsync(ctx, "second", [a]);

        (await LastAttachedAtAsync(ctx, a)).Should().Be(Base.UtcDateTime);
        (await LastAttachedAtAsync(ctx, b)).Should().Be(Base.UtcDateTime);
        (await LastAttachedAtAsync(ctx, c)).Should().BeNull();

        // Replace [a,b] with [b,c]: c is newly attached, a is detached, b is unchanged.
        var outcome = await ctx.Uow.ExecuteAsync(
            ct => ctx.Intents.SetTagsAsync(intent1.Id, intent1.State.CurrentVersion, [b, c], Base.AddMinutes(1), ct),
            CancellationToken.None);
        outcome.Should().BeOfType<SetIntentTagsOutcome.Updated>();

        (await LastAttachedAtAsync(ctx, a)).Should().Be(Base.UtcDateTime);
        (await LastAttachedAtAsync(ctx, b)).Should().Be(Base.UtcDateTime);
        (await LastAttachedAtAsync(ctx, c)).Should().Be(Base.AddMinutes(1).UtcDateTime);

        var second = await SeedIntentAsync(ctx, "third", [a]);
        (await LastAttachedAtAsync(ctx, a)).Should().Be(Base.UtcDateTime);
        await ctx.Uow.ExecuteAsync(ct => ctx.Intents.DeleteAsync(second.Id, ct), CancellationToken.None);
        (await LastAttachedAtAsync(ctx, a)).Should().Be(Base.UtcDateTime);
    }

    [Fact(DisplayName = "ListPageAsync сортирует по последней привязке, ищет по подстроке и пагинирует курсором")]
    public async Task ListPage_orders_by_last_attach_recency_with_search_and_cursor()
    {
        var ctx = await NewContextAsync();
        var older = await CreateTagAsync(ctx, "older-topic", Base.AddMinutes(-10));
        var fallback = await CreateTagAsync(ctx, "fallback-topic", Base.AddMinutes(-5));
        var cold = await CreateTagAsync(ctx, "cold");

        await SeedIntentAsync(ctx, "i1", [older], Base);
        await SeedIntentAsync(ctx, "i2", [cold], Base.AddMinutes(2));

        var page = await ctx.Tags.ListPageAsync(new TagListSpec(Search: null, Limit: 50, Cursor: null), CancellationToken.None);
        page.Items.Select(i => i.Tag.Id.Value).Should().Equal(cold.Value, older.Value, fallback.Value);
        page.NextCursor.Should().BeNull();

        var searched = await ctx.Tags.ListPageAsync(new TagListSpec(Search: "topic", Limit: 50, Cursor: null), CancellationToken.None);
        searched.Items.Select(i => i.Tag.Id.Value).Should().Equal(older.Value, fallback.Value);

        var first = await ctx.Tags.ListPageAsync(new TagListSpec(Search: null, Limit: 2, Cursor: null), CancellationToken.None);
        first.Items.Select(i => i.Tag.Id.Value).Should().Equal(cold.Value, older.Value);
        first.NextCursor.Should().NotBeNull();

        var second = await ctx.Tags.ListPageAsync(new TagListSpec(Search: null, Limit: 2, Cursor: first.NextCursor), CancellationToken.None);
        second.Items.Select(i => i.Tag.Id.Value).Should().Equal(fallback.Value);
        second.NextCursor.Should().BeNull();
    }

    private static async Task<DateTime?> LastAttachedAtAsync(TestContext ctx, TagId id)
    {
        var doc = await ctx.Database
            .GetCollection<TagDocument>(MongoCollectionNames.Tags)
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync();
        return doc!.LastAttachedAt;
    }

    private static async Task<TagId> CreateTagAsync(TestContext ctx, string name, DateTimeOffset? now = null)
    {
        var created = await ctx.Uow.ExecuteAsync(ct => ctx.Tags.CreateAsync(name, now ?? Base, ct), CancellationToken.None);
        return ((CreateTagOutcome.Created)created).Tag.Id;
    }

    private static async Task<Intent> SeedIntentAsync(
        TestContext ctx,
        string text,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset? now = null)
    {
        var createdAt = now ?? Base;
        var id = IntentId.New();
        var intent = Intent.Create(id, text, tagIds, createdAt);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, text, createdAt, TextVersionAuthor.Agent);
        var statusChange = IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"), id, intent.State.CurrentVersion,
            intent.State.Status, intent.State.Status, "test:create", createdAt, IntentTrainingAuthor.Agent);
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
