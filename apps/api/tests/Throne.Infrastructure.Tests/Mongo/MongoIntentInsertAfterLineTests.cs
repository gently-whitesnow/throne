using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentInsertAfterLineTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "InsertTextAfterLineAsync атомарно обновляет intents и пишет v2 в intent_events")]
    public async Task Insert_writes_v2()
    {
        var (db, repo, uow, id) = await SeedAsync("a\nb");

        var outcome = await uow.ExecuteAsync(
            ct => repo.InsertTextAfterLineAsync(id, expectedVersion: 1, afterLine: 1, "X\n", Edited, ct),
            CancellationToken.None);

        var inserted = outcome.Should().BeOfType<InsertIntentTextAfterLineOutcome.Inserted>().Subject;
        inserted.Intent.Text.Should().Be("a\nX\nb");
        inserted.Intent.CurrentVersion.Should().Be(2);

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(d => d.Id == id.Value).FirstOrDefaultAsync();
        stored!.Text.Should().Be("a\nX\nb");
        stored.CurrentVersion.Should().Be(2);

        var events = await db.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents)
            .Find(d => d.IntentId == id.Value).SortBy(d => d.Version).ToListAsync();
        events.Should().HaveCount(2);
        events[1].Kind.Should().Be("text_changed");
        events[1].TextChange!.Kind.Should().Be("insert");
        events[1].TextChange!.AfterLine.Should().Be(1);
        events[1].TextChange!.InsertText.Should().Be("X\n");
    }

    [Fact(DisplayName = "InsertTextAfterLineAsync с неверным expected_version возвращает VersionConflict без side-effects")]
    public async Task Wrong_expected_version_returns_conflict()
    {
        var (db, repo, uow, id) = await SeedAsync("a");

        var outcome = await uow.ExecuteAsync(
            ct => repo.InsertTextAfterLineAsync(id, expectedVersion: 99, afterLine: 0, "x", Edited, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<InsertIntentTextAfterLineOutcome.VersionConflict>();

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(d => d.Id == id.Value).FirstOrDefaultAsync();
        stored!.Text.Should().Be("a");
        stored.CurrentVersion.Should().Be(1);
        var events = await db.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents)
            .Find(d => d.IntentId == id.Value).ToListAsync();
        events.Should().HaveCount(1);
    }

    [Fact(DisplayName = "InsertTextAfterLineAsync с after_line вне диапазона возвращает LineOutOfRange")]
    public async Task Out_of_range_returns_line_out_of_range()
    {
        var (db, repo, uow, id) = await SeedAsync("a\nb");

        var outcome = await uow.ExecuteAsync(
            ct => repo.InsertTextAfterLineAsync(id, expectedVersion: 1, afterLine: 99, "x", Edited, ct),
            CancellationToken.None);

        var oor = outcome.Should().BeOfType<InsertIntentTextAfterLineOutcome.LineOutOfRange>().Subject;
        oor.TotalLines.Should().Be(2);
        oor.RequestedAfterLine.Should().Be(99);

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(d => d.Id == id.Value).FirstOrDefaultAsync();
        stored!.Text.Should().Be("a\nb");
        stored.CurrentVersion.Should().Be(1);
        var events = await db.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents)
            .Find(d => d.IntentId == id.Value).ToListAsync();
        events.Should().HaveCount(1);
    }

    private async Task<(IMongoDatabase Db, MongoIntentRepository Repo, IUnitOfWork Uow, IntentId Id)> SeedAsync(string text)
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor(), new MongoIntentEventRepository(db, sessions));
        var uow = new MongoUnitOfWork(fixture.Client, sessions);

        var id = IntentId.New();
        var intent = Intent.Create(id, "user-1", text, null, Created);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, text, Created, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);
        return (db, repo, uow, id);
    }

    private static IntentStatusChange InitialStatusChange(Intent intent) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.CurrentVersion,
            intent.Status,
            intent.Status,
            "test:create",
            Created,
            IntentTrainingAuthor.Agent);
}
