using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class TaskTrackerCardLinkRowConfiguration : IEntityTypeConfiguration<TaskTrackerCardLinkRow>
{
    public void Configure(EntityTypeBuilder<TaskTrackerCardLinkRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.TaskTrackers.CardLinks);
        builder.HasKey(x => x.IntentId);

        builder.Property(x => x.IntentId).HasColumnName("intent_id");
        builder.Property(x => x.Tracker).HasColumnName("tracker").IsRequired();
        builder.Property(x => x.BoardId).HasColumnName("board_id").IsRequired();
        builder.Property(x => x.CardId).HasColumnName("card_id").IsRequired();
        builder.Property(x => x.ColumnId).HasColumnName("column_id");
        builder.Property(x => x.ColumnTitle).HasColumnName("column_title");
        builder.Property(x => x.SnapshotTitle).HasColumnName("snapshot_title").IsRequired();
        builder.Property(x => x.SnapshotDescription).HasColumnName("snapshot_description");
        builder.Property(x => x.CardUpdatedAt).HasColumnName("card_updated_at");
        builder.Property(x => x.ColumnChangedAt).HasColumnName("column_changed_at");
        builder.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(x => x.RevisionTag).HasColumnName("revision_tag");
        builder.Property(x => x.State).HasColumnName("state").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.Tracker, x.BoardId, x.CardId })
            .IsUnique()
            .HasDatabaseName("tracker_board_card");
        builder.HasIndex(x => new { x.Tracker, x.BoardId })
            .HasDatabaseName("tracker_board");
    }
}
