using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class TextVersionRowConfiguration : IEntityTypeConfiguration<TextVersionRow>
{
    public void Configure(EntityTypeBuilder<TextVersionRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.TextVersions);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OwnerKind).HasColumnName("owner_kind").IsRequired();
        builder.Property(x => x.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version");
        builder.Property(x => x.Kind).HasColumnName("kind").IsRequired();
        builder.Property(x => x.Snapshot).HasColumnName("snapshot");
        builder.Property(x => x.OldText).HasColumnName("old_text");
        builder.Property(x => x.NewText).HasColumnName("new_text");
        builder.Property(x => x.AfterLine).HasColumnName("after_line");
        builder.Property(x => x.InsertText).HasColumnName("insert_text");
        builder.Property(x => x.ChangedAt).HasColumnName("changed_at");
        builder.Property(x => x.ChangedBy).HasColumnName("changed_by").IsRequired();

        builder.HasIndex(x => new { x.OwnerKind, x.OwnerId, x.Version })
            .IsUnique()
            .HasDatabaseName("ux_text_versions_owner_version");
    }
}
