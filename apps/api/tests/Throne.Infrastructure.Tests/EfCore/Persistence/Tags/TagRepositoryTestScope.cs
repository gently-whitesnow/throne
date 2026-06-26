using Throne.Application.Ports;
using Throne.Infrastructure.EfCore;

namespace Throne.Infrastructure.Tests.EfCore.Persistence.Tags;

internal sealed record TagRepositoryTestScope(
    SqliteTestDatabase Database,
    ITagRepository Repository,
    IUnitOfWork Uow)
{
    public static async Task<TagRepositoryTestScope> CreateAsync(SqliteFixture fixture)
    {
        var db = await fixture.CreateDatabaseAsync();
        return new TagRepositoryTestScope(
            db,
            db.GetRequiredService<ITagRepository>(),
            db.GetRequiredService<IUnitOfWork>());
    }
}
