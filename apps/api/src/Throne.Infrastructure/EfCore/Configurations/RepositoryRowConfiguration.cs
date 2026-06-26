using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class RepositoryRowConfiguration : IEntityTypeConfiguration<RepositoryRow>
{
    public void Configure(EntityTypeBuilder<RepositoryRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.Repositories);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Provider).HasColumnName("provider").IsRequired();
        // `host` is the effective host — GitHub collapses to "github.com" before INSERT —
        // so the unique index closes the «default vs explicit» race without a NULL column.
        builder.Property(x => x.Host).HasColumnName("host").IsRequired();
        builder.Property(x => x.Owner).HasColumnName("owner").IsRequired();
        builder.Property(x => x.Repo).HasColumnName("repo").IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => new { x.Provider, x.Host, x.Owner, x.Repo })
            .IsUnique()
            .HasDatabaseName("coordinate_unique");
    }
}
