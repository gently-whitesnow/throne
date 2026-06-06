using FluentAssertions;
using MongoDB.Bson;
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
public class TransactionRollbackTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ExecuteAsync коммитит обе записи, если лямбда завершилась без ошибки")]
    public async Task Commits_both_writes_on_success()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var id = IntentId.New();
        var intent = Intent.Create(id, "ok", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "ok", Now, TextVersionAuthor.Agent);

        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);

        (await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(x => x.Id == id.Value).AnyAsync()).Should().BeTrue();
        (await db.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents)
            .Find(x => x.IntentId == id.Value).AnyAsync()).Should().BeTrue();
    }

    [Fact(DisplayName = "ExecuteAsync откатывает первую запись, если вторая бросает")]
    public async Task Rolls_back_first_write_on_failure()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var id = IntentId.New();
        var intent = Intent.Create(id, "boom", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "boom", Now, TextVersionAuthor.Agent);

        var act = async () => await uow.ExecuteAsync(async ct =>
        {
            await repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct);
            throw new InvalidOperationException("boom");
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        (await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(x => x.Id == id.Value).AnyAsync()).Should().BeFalse();
        (await db.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents)
            .Find(x => x.IntentId == id.Value).AnyAsync()).Should().BeFalse();
    }

    [Fact(DisplayName = "ExecuteAsync ретраит WriteConflict: конкурентные транзакции на одном документе все коммитятся")]
    public async Task Retries_write_conflict_under_concurrency()
    {
        var (db, _, _) = await NewScopeAsync();
        var sessions = new MongoSessionAccessor();
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        var counters = db.GetCollection<BsonDocument>("counters");
        var byId = Builders<BsonDocument>.Filter.Eq("_id", "c");
        await counters.InsertOneAsync(new BsonDocument { { "_id", "c" }, { "v", 0 } });

        // 8 транзакций делают read-modify-write одного документа: под snapshot-изоляцией
        // они конфликтуют (WriteConflict), и пройти все могут только через клиентский retry.
        const int writers = 8;
        var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(() =>
            uow.ExecuteAsync(async ct =>
            {
                var session = sessions.Current!;
                var doc = await counters.Find(session, byId).FirstAsync(ct);
                await counters.UpdateOneAsync(
                    session, byId, Builders<BsonDocument>.Update.Set("v", doc["v"].AsInt32 + 1), cancellationToken: ct);
            }, CancellationToken.None))).ToArray();

        await Task.WhenAll(tasks);

        (await counters.Find(byId).FirstAsync())["v"].AsInt32.Should().Be(writers);
    }

    private async Task<(IMongoDatabase Db, MongoIntentRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoIntentRepository(db, sessions, new MongoIntentEventRepository(db, sessions));
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return (db, repo, uow);
    }

    private static IntentStatusChange InitialStatusChange(Intent intent) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.State.CurrentVersion,
            intent.State.Status,
            intent.State.Status,
            "test:create",
            Now,
            IntentTrainingAuthor.Agent);
}
