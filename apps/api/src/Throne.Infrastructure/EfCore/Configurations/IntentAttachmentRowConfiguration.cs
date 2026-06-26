using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentAttachmentRowConfiguration : IEntityTypeConfiguration<IntentAttachmentRow>
{
    public void Configure(EntityTypeBuilder<IntentAttachmentRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.IntentAttachments);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IntentId).HasColumnName("intent_id").IsRequired();
        builder.Property(x => x.FileName).HasColumnName("file_name").IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CompressionState).HasColumnName("compression_state");
        builder.Property(x => x.DerivedWidth).HasColumnName("derived_width");
        builder.Property(x => x.DerivedHeight).HasColumnName("derived_height");
        // BLOB column for the attachment payload — kept on the same row so per-intent
        // delete/list paths stay single-statement.
        builder.Property(x => x.ContentBytes)
            .HasColumnName("content_bytes")
            .HasColumnType("BLOB")
            .IsRequired();

        // SQLite indexes share a single namespace, so qualify the per-table «intent_id»
        // name to avoid a collision with the bindings table's own «intent_id» index.
        builder.HasIndex(x => x.IntentId).HasDatabaseName("ix_intent_attachments_intent_id");
    }
}
