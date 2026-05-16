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
public class TransactionRollbackTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ExecuteAsync коммитит обе записи, если лямбда завершилась без ошибки")]
    public async Task Commits_both_writes_on_success()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var id = IntentId.New();
        var intent = IntentFactory.Create(id, "user-1", "ok", null, Now);
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
        var intent = IntentFactory.Create(id, "user-1", "boom", null, Now);
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

    private async Task<(IMongoDatabase Db, MongoIntentRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor(), new MongoIntentEventRepository(db, sessions));
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
