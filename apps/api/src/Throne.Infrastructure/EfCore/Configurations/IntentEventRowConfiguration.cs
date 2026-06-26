using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentEventRowConfiguration : IEntityTypeConfiguration<IntentEventRow>
{
    public void Configure(EntityTypeBuilder<IntentEventRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Intents.IntentEvents);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IntentId).HasColumnName("intent_id").IsRequired();
        builder.Property(x => x.PeerIntentId).HasColumnName("peer_intent_id");
        builder.Property(x => x.Kind).HasColumnName("kind").IsRequired();
        builder.Property(x => x.Version).HasColumnName("version");

        builder.Property(x => x.TextChange)
            .HasColumnName("text_change")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<IntentEventTextChange>(v, EfJson.Options));

        builder.Property(x => x.Link)
            .HasColumnName("link")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<IntentEventLinkPayload>(v, EfJson.Options));

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");

        builder.HasIndex(x => new { x.IntentId, x.CreatedAt }).HasDatabaseName("ix_intent_events_intent_created");
        builder.HasIndex(x => x.PeerIntentId).HasDatabaseName("ix_intent_events_peer_intent_id");
        // Partial unique index: exactly one text_changed event per (intent_id, version).
        // Link events leave version null and would otherwise collide.
        builder.HasIndex(x => new { x.IntentId, x.Version })
            .IsUnique()
            .HasFilter($"kind = '{IntentEventKindWires.TextChanged}'")
            .HasDatabaseName("ux_intent_events_intent_text_version");
    }
}
