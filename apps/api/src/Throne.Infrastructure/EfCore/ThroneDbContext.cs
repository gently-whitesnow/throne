using Microsoft.EntityFrameworkCore;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core context for the SQLite backend. Entity mappings live in
/// <see cref="IEntityTypeConfiguration{TEntity}"/> classes discovered via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>; the context itself stays
/// free of inline configuration.
/// </summary>
public sealed class ThroneDbContext(DbContextOptions<ThroneDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThroneDbContext).Assembly);
    }
}
