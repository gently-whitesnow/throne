using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class PromptPartRowConfiguration : IEntityTypeConfiguration<PromptPartRow>
{
    public void Configure(EntityTypeBuilder<PromptPartRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.PromptParts.Table);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Scope).HasColumnName("scope").IsRequired();
        builder.Property(x => x.Key).HasColumnName("key").IsRequired();
        builder.Property(x => x.Text).HasColumnName("text").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        // CAS guard for ReplaceText / SetModeRoles / Delete — every write filters on
        // current_version and inspects ExecuteUpdateAsync's affected-rows count.
        // IsConcurrencyToken keeps EF's tracker aligned with the manual CAS.
        builder.Property(x => x.CurrentVersion)
            .HasColumnName("current_version")
            .IsConcurrencyToken();

        builder.Property(x => x.ModeRoles)
            .HasColumnName("mode_roles")
            .HasConversion(
                v => JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? new List<PromptPartModeRolePayload>()
                    : JsonSerializer.Deserialize<List<PromptPartModeRolePayload>>(v, EfJson.Options)
                        ?? new List<PromptPartModeRolePayload>(),
                new ValueComparer<List<PromptPartModeRolePayload>>(
                    (l, r) => ReferenceEquals(l, r)
                        || (l != null && r != null
                            && JsonSerializer.Serialize(l, EfJson.Options)
                                == JsonSerializer.Serialize(r, EfJson.Options)),
                    v => v == null ? 0 : JsonSerializer.Serialize(v, EfJson.Options).GetHashCode(StringComparison.Ordinal),
                    v => JsonSerializer.Deserialize<List<PromptPartModeRolePayload>>(
                            JsonSerializer.Serialize(v, EfJson.Options), EfJson.Options)
                        ?? new List<PromptPartModeRolePayload>()))
            .IsRequired();

        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        // Unique on the (scope, key) identity tuple; conflict path returns
        // CreatePromptPartOutcome.KeyConflict.
        builder.HasIndex(x => new { x.Scope, x.Key })
            .IsUnique()
            .HasDatabaseName("scope_key_unique");
    }
}
