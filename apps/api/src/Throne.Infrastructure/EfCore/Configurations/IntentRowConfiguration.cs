using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentRowConfiguration : IEntityTypeConfiguration<IntentRow>
{
    public void Configure(EntityTypeBuilder<IntentRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Intents.Table);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Text).HasColumnName("text").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        // CAS guard: every status/text/tags write filters on current_version and we double-check
        // the affected-rows count from ExecuteUpdateAsync; IsConcurrencyToken keeps EF's own
        // tracking aligned with the manual CAS.
        builder.Property(x => x.CurrentVersion)
            .HasColumnName("current_version")
            .IsConcurrencyToken();

        builder.Property(x => x.TagIds)
            .HasColumnName("tag_ids")
            .HasConversion(
                v => JsonSerializer.Serialize(v, EfJson.Options),
                v => JsonSerializer.Deserialize<List<string>>(v, EfJson.Options) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (l, r) => (l ?? new List<string>()).SequenceEqual(r ?? new List<string>()),
                    v => v.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode(StringComparison.Ordinal))),
                    v => v.ToList()))
            .IsRequired();

        builder.Property(x => x.SortKey).HasColumnName("sort_key").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CleanupLocalStateOnDone)
            .HasColumnName("cleanup_local_state_on_done")
            .HasDefaultValue(true);

        builder.HasIndex(x => x.SortKey).HasDatabaseName("ix_intents_sort_key");
        builder.HasIndex(x => new { x.UpdatedAt, x.Id }).HasDatabaseName("ix_intents_updated_at_id");
        builder.HasIndex(x => new { x.CreatedAt, x.Id }).HasDatabaseName("ix_intents_created_at_id");
    }
}
