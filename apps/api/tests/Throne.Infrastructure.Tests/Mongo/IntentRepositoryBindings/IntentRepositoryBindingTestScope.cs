using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Repositories;

namespace Throne.Infrastructure.Tests.Mongo.IntentRepositoryBindings;

/// <summary>
/// Per-test scope: fresh database + repository instance. Indexes are created up-front so
/// every test runs against the same schema the production
/// <c>MongoIndexInitializer</c> would build.
/// </summary>
internal sealed record IntentRepositoryBindingTestScope(
    IMongoDatabase Database,
    IIntentRepositoryBindingRepository Repository,
    IUnitOfWork UnitOfWork)
{
    public static async Task<IntentRepositoryBindingTestScope> CreateAsync(MongoFixture fixture)
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);

        await MongoIntentRepositoryBindingIndexes.CreateAsync(db, CancellationToken.None);

        var sessions = new MongoSessionAccessor();
        var repo = new MongoIntentRepositoryBindingStore(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return new IntentRepositoryBindingTestScope(db, repo, uow);
    }
}

internal static class IntentRepositoryBindingTestFactory
{
    public static readonly DateTimeOffset Now = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    public static IntentRepositoryBinding NewBinding(
        IntentId intentId,
        string owner = "octo",
        string repo = "throne",
        int? prNumber = null,
        DateTimeOffset? at = null) =>
        IntentRepositoryBindingFactory.Create(
            id: BindingId.New(),
            intentId: intentId,
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, owner, repo),
            defaultBranch: "main",
            workspacePath: $"/tmp/throne/{intentId.Value}/{owner}__{repo}",
            pullRequestNumber: prNumber,
            now: at ?? Now);
}
