using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class PromptPartPatchRowConfiguration : IEntityTypeConfiguration<PromptPartPatchRow>
{
    public void Configure(EntityTypeBuilder<PromptPartPatchRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.PromptParts.PromptPartPatches);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TargetScope).HasColumnName("target_scope").IsRequired();
        builder.Property(x => x.TargetKey).HasColumnName("target_key").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.Operation).HasColumnName("operation").IsRequired();
        builder.Property(x => x.PatchText).HasColumnName("patch_text").IsRequired();

        builder.Property(x => x.ModeRoles)
            .HasColumnName("mode_roles")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? null
                    : JsonSerializer.Deserialize<List<PromptPartModeRolePayload>>(v, EfJson.Options));

        builder.Property(x => x.AppliedText).HasColumnName("applied_text");
        builder.Property(x => x.Rationale).HasColumnName("rationale").IsRequired();
        builder.Property(x => x.RejectComment).HasColumnName("reject_comment");
        builder.Property(x => x.BaseVersion).HasColumnName("base_version");
        builder.Property(x => x.AppliedVersion).HasColumnName("applied_version");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DecidedAt).HasColumnName("decided_at");
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");

        // Drives paginated listings (created_at desc, id desc — sort/cursor in the reader).
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("created_desc");
        // (status, created_at) backs status-filtered list calls.
        builder.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("status_created");
        // (target_scope, target_key, created_at) backs per-target audit listings.
        builder.HasIndex(x => new { x.TargetScope, x.TargetKey, x.CreatedAt }).HasDatabaseName("target_created");
        // Partial unique on idempotency_key — only rows that actually carry a key
        // participate so non-idempotent legacy proposals never collide on NULL.
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL")
            .HasDatabaseName("idempotency_key_unique");
    }
}
