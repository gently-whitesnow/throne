using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class GitLabHostSettingsRowConfiguration : IEntityTypeConfiguration<GitLabHostSettingsRow>
{
    public void Configure(EntityTypeBuilder<GitLabHostSettingsRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.GitLabHostSettings);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Host).HasColumnName("host").IsRequired();
    }
}
