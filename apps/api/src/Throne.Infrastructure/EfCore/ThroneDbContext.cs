using Microsoft.EntityFrameworkCore;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core context for the SQLite backend. Entity mappings live in
/// <see cref="IEntityTypeConfiguration{TEntity}"/> classes discovered via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>; the context itself stays
/// free of inline configuration. Slice 1 ships with no configurations yet (the
/// repository ports and their mappings arrive in slice 2), so the model is empty and
/// the initial migration is empty by design.
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
