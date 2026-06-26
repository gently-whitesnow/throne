using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentLinkRowConfiguration : IEntityTypeConfiguration<IntentLinkRow>
{
    public void Configure(EntityTypeBuilder<IntentLinkRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.IntentLinks);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FromId).HasColumnName("from_id").IsRequired();
        builder.Property(x => x.ToId).HasColumnName("to_id").IsRequired();
        builder.Property(x => x.Blocking).HasColumnName("blocking");
        builder.Property(x => x.Author).HasColumnName("author").IsRequired();
        builder.Property(x => x.Rationale).HasColumnName("rationale");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(x => new { x.FromId, x.ToId })
            .IsUnique()
            .HasDatabaseName("ux_intent_links_from_to");
        builder.HasIndex(x => x.FromId).HasDatabaseName("ix_intent_links_from_id");
        builder.HasIndex(x => x.ToId).HasDatabaseName("ix_intent_links_to_id");
    }
}
