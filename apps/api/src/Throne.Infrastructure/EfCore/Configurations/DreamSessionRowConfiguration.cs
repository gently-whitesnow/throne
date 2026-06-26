using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class DreamSessionRowConfiguration : IEntityTypeConfiguration<DreamSessionRow>
{
    public void Configure(EntityTypeBuilder<DreamSessionRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Dreams.DreamSessions);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.Vendor).HasColumnName("vendor").IsRequired();
        builder.Property(x => x.Host).HasColumnName("host");
        builder.Property(x => x.DateFrom).HasColumnName("date_from");
        builder.Property(x => x.DateTo).HasColumnName("date_to");
        builder.Property(x => x.Summary).HasColumnName("summary").IsRequired();
        builder.Property(x => x.Reflection).HasColumnName("reflection");

        builder.Property(x => x.ProcessedConversationIds)
            .HasColumnName("processed_conversation_ids")
            .HasConversion(StringListConverter())
            .Metadata.SetValueComparer(StringListComparer());

        builder.Property(x => x.ProposedPatchIds)
            .HasColumnName("proposed_patch_ids")
            .HasConversion(StringListConverter())
            .Metadata.SetValueComparer(StringListComparer());

        // Index names are globally unique per SQLite database — prefix with the table so
        // they don't collide with the same-shape index on prompt_part_patches.
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("dream_sessions_created_desc");
        builder.HasIndex(x => new { x.Vendor, x.CreatedAt }).HasDatabaseName("dream_sessions_vendor_created");
        builder.HasIndex(x => new { x.Host, x.CreatedAt }).HasDatabaseName("dream_sessions_host_created");
    }

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<List<string>, string>
        StringListConverter() => new(
            v => JsonSerializer.Serialize(v, EfJson.Options),
            v => string.IsNullOrEmpty(v)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(v, EfJson.Options) ?? new List<string>());

    private static ValueComparer<List<string>> StringListComparer() => new(
        (l, r) => (l ?? new List<string>()).SequenceEqual(r ?? new List<string>()),
        v => v.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode(StringComparison.Ordinal))),
        v => v.ToList());
}
