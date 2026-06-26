using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentPinRowConfiguration : IEntityTypeConfiguration<IntentPinRow>
{
    public void Configure(EntityTypeBuilder<IntentPinRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.IntentPins);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IntentId).HasColumnName("intent_id").IsRequired();
        builder.Property(x => x.ContextTagId).HasColumnName("context_tag_id").IsRequired();
        builder.Property(x => x.PinSortKey).HasColumnName("pin_sort_key").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        // One pin per (intent, context). Race-protects the upsert path.
        builder.HasIndex(x => new { x.IntentId, x.ContextTagId })
            .IsUnique()
            .HasDatabaseName("ux_intent_pins_intent_context");
        builder.HasIndex(x => new { x.ContextTagId, x.PinSortKey })
            .HasDatabaseName("ix_intent_pins_context_sort_key");
        builder.HasIndex(x => x.IntentId).HasDatabaseName("ix_intent_pins_intent");
    }
}
