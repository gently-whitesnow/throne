using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class TerminalSettingsRowConfiguration : IEntityTypeConfiguration<TerminalSettingsRow>
{
    public void Configure(EntityTypeBuilder<TerminalSettingsRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Terminals.TerminalSettings);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.DefaultVendor).HasColumnName("default_vendor").IsRequired();
    }
}
