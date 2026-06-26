using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class PullRequestArtifactRowConfiguration : IEntityTypeConfiguration<PullRequestArtifactRow>
{
    public void Configure(EntityTypeBuilder<PullRequestArtifactRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.PullRequestArtifacts);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BindingId).HasColumnName("binding_id").IsRequired();
        builder.Property(x => x.PullRequestNumber).HasColumnName("pull_request_number");
        builder.Property(x => x.Type).HasColumnName("type").IsRequired();
        builder.Property(x => x.Render).HasColumnName("render").IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").IsRequired();
        builder.Property(x => x.Summary).HasColumnName("summary").IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").IsRequired();
        builder.Property(x => x.ProducedAt).HasColumnName("produced_at");
        builder.Property(x => x.HeadSha).HasColumnName("head_sha");

        builder.Property(x => x.SourceRefs)
            .HasColumnName("source_refs")
            .HasConversion(
                v => JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(v, EfJson.Options) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (l, r) => (l ?? new List<string>()).SequenceEqual(r ?? new List<string>()),
                    v => v.Aggregate(0, (a, s) => HashCode.Combine(a, s.GetHashCode(StringComparison.Ordinal))),
                    v => v.ToList()))
            .IsRequired();

        builder.Property(x => x.ReviewRecommendation)
            .HasColumnName("review_recommendation")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, EfJson.Options),
                v => string.IsNullOrEmpty(v)
                    ? null
                    : JsonSerializer.Deserialize<ReviewRecommendationPayload>(v, EfJson.Options));

        builder.HasIndex(x => new { x.BindingId, x.Type })
            .IsUnique()
            .HasDatabaseName("binding_type_unique");
        builder.HasIndex(x => x.BindingId).HasDatabaseName("binding_id");
    }
}
