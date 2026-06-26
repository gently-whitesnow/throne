using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.Mongo.IntentRepositoryBindings;

/// <summary>
/// Per-test scope: fresh migrated SQLite database + repository instance.
/// </summary>
internal sealed record IntentRepositoryBindingTestScope(
    SqliteTestDatabase Database,
    IIntentRepositoryBindingRepository Repository,
    IUnitOfWork UnitOfWork)
{
    public static async Task<IntentRepositoryBindingTestScope> CreateAsync(SqliteFixture fixture)
    {
        var db = await fixture.CreateDatabaseAsync();
        return new IntentRepositoryBindingTestScope(
            db,
            db.GetRequiredService<IIntentRepositoryBindingRepository>(),
            db.GetRequiredService<IUnitOfWork>());
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
        IntentRepositoryBinding.Create(
            id: BindingId.New(),
            intentId: intentId,
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, owner, repo),
            defaultBranch: "main",
            workspacePath: $"/tmp/throne/{intentId.Value}/{owner}__{repo}",
            pullRequestNumber: prNumber,
            now: at ?? Now);
}
