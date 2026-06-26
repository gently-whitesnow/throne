using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Configurations;

internal sealed class IntentRepositoryBindingRowConfiguration : IEntityTypeConfiguration<IntentRepositoryBindingRow>
{
    public void Configure(EntityTypeBuilder<IntentRepositoryBindingRow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable(EfTableNames.IntentRepositoryBindings);
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.IntentId).HasColumnName("intent_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").IsRequired();
        builder.Property(x => x.Host).HasColumnName("host").IsRequired();
        builder.Property(x => x.Owner).HasColumnName("owner").IsRequired();
        builder.Property(x => x.Repo).HasColumnName("repo").IsRequired();
        builder.Property(x => x.ProjectId).HasColumnName("project_id");
        builder.Property(x => x.DefaultBranch).HasColumnName("default_branch").IsRequired();
        builder.Property(x => x.WorkspacePath).HasColumnName("workspace_path").IsRequired();
        builder.Property(x => x.CloneStatus).HasColumnName("clone_status").IsRequired();
        builder.Property(x => x.CloneError).HasColumnName("clone_error");
        builder.Property(x => x.PullRequestNumber).HasColumnName("pull_request_number");
        builder.Property(x => x.PullRequestState).HasColumnName("pull_request_state");
        builder.Property(x => x.ReviewCommentsEtag).HasColumnName("review_comments_etag");
        builder.Property(x => x.LastSeenReviewCommentAt).HasColumnName("last_seen_review_comment_at");
        builder.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.SuppressMergeAutoClose)
            .HasColumnName("suppress_merge_auto_close")
            .HasDefaultValue(false);

        // UNIQUE one bind per coordinate per intent — closes the duplicate-bind race so the
        // 409 surface never depends on an in-process lookup.
        builder.HasIndex(x => new { x.IntentId, x.Provider, x.Host, x.Owner, x.Repo })
            .IsUnique()
            .HasDatabaseName("intent_coordinate_unique");
        builder.HasIndex(x => x.IntentId).HasDatabaseName("intent_id");
        builder.HasIndex(x => new { x.PullRequestState, x.LastSyncedAt })
            .HasDatabaseName("pr_state_last_synced_at");
        builder.HasIndex(x => new { x.CloneStatus, x.PullRequestNumber })
            .HasDatabaseName("clone_status_pr_number");
    }
}
