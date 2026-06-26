using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.EfCore.Persistence.Repositories;

/// <summary>
/// Per-test scope: fresh migrated SQLite database + the registry / PR-artifact stores
/// wired through the production EF Core module.
/// </summary>
internal sealed record RepositoryStoreTestScope(
    SqliteTestDatabase Database,
    IRepositoryRegistry Registry,
    IPullRequestArtifactRepository PullRequestArtifacts,
    IUnitOfWork UnitOfWork)
{
    public static async Task<RepositoryStoreTestScope> CreateAsync(SqliteFixture fixture)
    {
        var db = await fixture.CreateDatabaseAsync();
        return new RepositoryStoreTestScope(
            db,
            db.GetRequiredService<IRepositoryRegistry>(),
            db.GetRequiredService<IPullRequestArtifactRepository>(),
            db.GetRequiredService<IUnitOfWork>());
    }
}
