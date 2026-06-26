using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Lets <c>dotnet ef</c> build a <see cref="ThroneDbContext"/> without booting the host.
/// Uses the default <see cref="EfPersistenceOptions"/> data source; migrations scaffolding
/// only needs the model and provider, not a live connection.
/// </summary>
public sealed class ThroneDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ThroneDbContext>
{
    public ThroneDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ThroneDbContext>()
            .UseSqlite(EfSqliteConnectionString.For(new EfPersistenceOptions()))
            .Options;
        return new ThroneDbContext(options);
    }
}
