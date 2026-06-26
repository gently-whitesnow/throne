using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class TagRowConfiguration : IEntityTypeConfiguration<TagRow>
{
    public void Configure(EntityTypeBuilder<TagRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Tags.Table);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        // CAS guard for Rename / SetDefaultRepositories — every write filters on current_version
        // and inspects the affected-rows count. IsConcurrencyToken keeps EF's own tracker aligned
        // with the manual CAS so an accidental SaveChanges path would also surface conflicts.
        builder.Property(x => x.CurrentVersion)
            .HasColumnName("current_version")
            .IsConcurrencyToken();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.LastAttachedAt).HasColumnName("last_attached_at");

        builder.Property(x => x.DefaultRepositories)
            .HasColumnName("default_repositories")
            .HasConversion(
                v => JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? new List<TagDefaultRepositoryPayload>()
                    : JsonSerializer.Deserialize<List<TagDefaultRepositoryPayload>>(v, EfJson.Options)
                        ?? new List<TagDefaultRepositoryPayload>(),
                new ValueComparer<List<TagDefaultRepositoryPayload>>(
                    (l, r) => ReferenceEquals(l, r)
                        || (l != null && r != null
                            && JsonSerializer.Serialize(l, EfJson.Options)
                                == JsonSerializer.Serialize(r, EfJson.Options)),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, EfJson.Options).GetHashCode(StringComparison.Ordinal),
                    v => JsonSerializer.Deserialize<List<TagDefaultRepositoryPayload>>(
                            JsonSerializer.Serialize(v, EfJson.Options), EfJson.Options)
                        ?? new List<TagDefaultRepositoryPayload>()))
            .IsRequired();

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("name_unique");
        builder.HasIndex(x => new { x.LastAttachedAt, x.Id })
            .HasFilter("last_attached_at IS NOT NULL")
            .HasDatabaseName("last_attached_at_id");
        builder.HasIndex(x => new { x.CreatedAt, x.Id })
            .HasDatabaseName("created_at_id");
    }
}
