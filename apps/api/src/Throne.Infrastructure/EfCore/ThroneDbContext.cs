using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core context for the SQLite backend. Entity mappings live in
/// <see cref="IEntityTypeConfiguration{TEntity}"/> classes discovered via
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>; the context itself stays
/// free of inline configuration.
/// </summary>
public sealed class ThroneDbContext(DbContextOptions<ThroneDbContext> options) : DbContext(options)
{
    private static readonly ValueConverter<DateTimeOffset, string> DateTimeOffsetTextConverter = new(
        v => v.ToString("O", CultureInfo.InvariantCulture),
        v => DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static readonly ValueConverter<DateTimeOffset?, string?> NullableDateTimeOffsetTextConverter = new(
        v => v.HasValue ? v.Value.ToString("O", CultureInfo.InvariantCulture) : null,
        v => string.IsNullOrEmpty(v)
            ? null
            : DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThroneDbContext).Assembly);
        ApplyDateTimeOffsetTextConverters(modelBuilder);
    }

    private static void ApplyDateTimeOffsetTextConverters(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTimeOffset))
            {
                property.SetValueConverter(DateTimeOffsetTextConverter);
            }
            else if (property.ClrType == typeof(DateTimeOffset?))
            {
                property.SetValueConverter(NullableDateTimeOffsetTextConverter);
            }
        }
    }
}
