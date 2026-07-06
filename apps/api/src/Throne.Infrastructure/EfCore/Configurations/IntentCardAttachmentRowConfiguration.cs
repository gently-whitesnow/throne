using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentCardAttachmentRowConfiguration : IEntityTypeConfiguration<IntentCardAttachmentRow>
{
    public void Configure(EntityTypeBuilder<IntentCardAttachmentRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.TaskTrackers.IntentCardAttachments);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IntentId).HasColumnName("intent_id").IsRequired();
        builder.Property(x => x.Tracker).HasColumnName("tracker").IsRequired();
        builder.Property(x => x.BoardId).HasColumnName("board_id").IsRequired();
        builder.Property(x => x.CardId).HasColumnName("card_id").IsRequired();
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.ColumnTitle).HasColumnName("column_title");
        builder.Property(x => x.Archived).HasColumnName("archived");
        builder.Property(x => x.CardVersion).HasColumnName("card_version");
        builder.Property(x => x.Availability).HasColumnName("availability").IsRequired();
        builder.Property(x => x.FetchedAt).HasColumnName("fetched_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // UNIQUE one attachment per card per intent — closes the re-attach race so idempotency never
        // depends on an in-process lookup.
        // SQLite index names are database-global, so every index carries the table prefix to avoid
        // colliding with another table's index of the same shorthand (e.g. binding's «intent_id»).
        builder.HasIndex(x => new { x.IntentId, x.Tracker, x.BoardId, x.CardId })
            .IsUnique()
            .HasDatabaseName("intent_card_unique");
        builder.HasIndex(x => x.IntentId).HasDatabaseName("ix_intent_card_attachments_intent_id");
        // Board read-path filter: «intents attached to (tracker, board_id)».
        builder.HasIndex(x => new { x.Tracker, x.BoardId })
            .HasDatabaseName("ix_intent_card_attachments_board");
    }
}
