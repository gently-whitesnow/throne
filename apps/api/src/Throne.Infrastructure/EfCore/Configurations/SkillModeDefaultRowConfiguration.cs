using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class SkillModeDefaultRowConfiguration : IEntityTypeConfiguration<SkillModeDefaultRow>
{
    public void Configure(EntityTypeBuilder<SkillModeDefaultRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.SkillModeDefaults);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Mode).HasColumnName("mode").IsRequired();
        builder.Property(x => x.SkillId).HasColumnName("skill_id").IsRequired();
        builder.Property(x => x.Enabled).HasColumnName("enabled");

        // UNIQUE (mode, skill_id) closes the seeder/replace race so the same (mode, skill)
        // can't end up twice from a concurrent boot pass.
        builder.HasIndex(x => new { x.Mode, x.SkillId })
            .IsUnique()
            .HasDatabaseName("mode_skill_unique");
    }
}
