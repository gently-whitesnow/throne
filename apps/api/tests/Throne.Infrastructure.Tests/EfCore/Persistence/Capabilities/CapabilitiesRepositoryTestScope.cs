using Throne.Application.Ports;
using Throne.Infrastructure.EfCore;

namespace Throne.Infrastructure.Tests.EfCore.Persistence.Capabilities;

internal sealed record CapabilitiesRepositoryTestScope(
    SqliteTestDatabase Database,
    ICapabilitiesRepository Repository,
    IUnitOfWork Uow)
{
    public static async Task<CapabilitiesRepositoryTestScope> CreateAsync(SqliteFixture fixture)
    {
        var db = await fixture.CreateDatabaseAsync();
        return new CapabilitiesRepositoryTestScope(
            db,
            db.GetRequiredService<ICapabilitiesRepository>(),
            db.GetRequiredService<IUnitOfWork>());
    }
}
