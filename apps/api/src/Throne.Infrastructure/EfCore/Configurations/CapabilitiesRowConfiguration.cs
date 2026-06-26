using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class CapabilitiesRowConfiguration : IEntityTypeConfiguration<CapabilitiesRow>
{
    public void Configure(EntityTypeBuilder<CapabilitiesRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Capabilities);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        // Concurrency token: the singleton aggregate increments CurrentVersion on every
        // mutation, so a stale write from a concurrent operator session loses cleanly.
        builder.Property(x => x.CurrentVersion)
            .HasColumnName("current_version")
            .IsConcurrencyToken();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.Property(x => x.Selections)
            .HasColumnName("selections")
            .HasConversion(
                v => JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(v, EfJson.Options)
                        ?? new Dictionary<string, string>(StringComparer.Ordinal),
                new ValueComparer<Dictionary<string, string>>(
                    (l, r) => DictEquals(l, r),
                    v => v == null
                        ? 0
                        : v.Aggregate(0, (a, kv) => HashCode.Combine(
                            a,
                            kv.Key.GetHashCode(StringComparison.Ordinal),
                            kv.Value.GetHashCode(StringComparison.Ordinal))),
                    v => new Dictionary<string, string>(v, StringComparer.Ordinal)))
            .IsRequired();
    }

    private static bool DictEquals(Dictionary<string, string>? l, Dictionary<string, string>? r)
    {
        if (l is null && r is null)
        {
            return true;
        }
        if (l is null || r is null || l.Count != r.Count)
        {
            return false;
        }
        foreach (var (k, v) in l)
        {
            if (!r.TryGetValue(k, out var other) || !string.Equals(v, other, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }
}
