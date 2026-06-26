using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentStatusChangeRowConfiguration : IEntityTypeConfiguration<IntentStatusChangeRow>
{
    public void Configure(EntityTypeBuilder<IntentStatusChangeRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Intents.IntentStatusChanges);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IntentId).HasColumnName("intent_id").IsRequired();
        builder.Property(x => x.IntentVersionAtWrite).HasColumnName("intent_version_at_write");
        builder.Property(x => x.FromStatus).HasColumnName("from_status").IsRequired();
        builder.Property(x => x.ToStatus).HasColumnName("to_status").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason");

        builder.HasIndex(x => new { x.IntentId, x.CreatedAt })
            .HasDatabaseName("ix_intent_status_changes_intent_created");
    }
}
