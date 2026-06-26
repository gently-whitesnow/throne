using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentTerminalLaunchRowConfiguration : IEntityTypeConfiguration<IntentTerminalLaunchRow>
{
    public void Configure(EntityTypeBuilder<IntentTerminalLaunchRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Terminals.TerminalLaunches);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Mode).HasColumnName("mode").IsRequired();
        builder.Property(x => x.Vendor).HasColumnName("vendor").IsRequired();
        builder.Property(x => x.Model).HasColumnName("model").IsRequired();
        // Effort is NULL for effortless vendors (opencode); a later switch to an effort
        // vendor never reads a stale value because the column drops to NULL on Unset.
        builder.Property(x => x.Effort).HasColumnName("effort");

        builder.Property(x => x.AttachedSkillIds)
            .HasColumnName("attached_skill_ids")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(v, EfJson.Options),
                new ValueComparer<List<string>?>(
                    (l, r) => SequenceEquals(l, r),
                    v => v == null
                        ? 0
                        : v.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode(StringComparison.Ordinal))),
                    v => v == null ? null : v.ToList()));

        builder.Property(x => x.SelectedSkillIdsByMode)
            .HasColumnName("selected_skill_ids_by_mode")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(v, EfJson.Options),
                new ValueComparer<Dictionary<string, List<string>>?>(
                    (l, r) => DictSeqEquals(l, r),
                    v => v == null
                        ? 0
                        : v.Aggregate(0, (a, kv) => HashCode.Combine(a, kv.Key.GetHashCode(StringComparison.Ordinal))),
                    v => v == null
                        ? null
                        : v.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal)));
    }

    private static bool SequenceEquals(List<string>? l, List<string>? r)
    {
        if (l is null && r is null)
        {
            return true;
        }
        if (l is null || r is null)
        {
            return false;
        }
        return l.SequenceEqual(r);
    }

    private static bool DictSeqEquals(
        Dictionary<string, List<string>>? l, Dictionary<string, List<string>>? r)
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
            if (!r.TryGetValue(k, out var other) || !v.SequenceEqual(other))
            {
                return false;
            }
        }
        return true;
    }
}
