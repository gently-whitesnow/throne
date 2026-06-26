using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentRepositoryBindingRowMapper
{
    public static IntentRepositoryBindingRow ToRow(IntentRepositoryBinding binding) => new()
    {
        Id = binding.Id.Value,
        IntentId = binding.IntentId.Value,
        Provider = binding.Coordinate.Provider,
        // RepoCoordinate already normalizes Host (GitHub → "github.com"). Storing the normalized
        // value lets the unique index treat «default» and «explicit github.com» as the same row.
        Host = binding.Coordinate.Host,
        Owner = binding.Coordinate.Owner,
        Repo = binding.Coordinate.Repo,
        ProjectId = binding.Coordinate.ProjectId,
        DefaultBranch = binding.State.DefaultBranch,
        WorkspacePath = binding.WorkspacePath,
        CloneStatus = binding.State.CloneStatus,
        CloneError = binding.State.CloneError,
        PullRequestNumber = binding.State.PullRequestNumber,
        PullRequestState = binding.State.PullRequestState,
        ReviewCommentsEtag = binding.State.ReviewCommentsEtag,
        LastSeenReviewCommentAt = binding.State.LastSeenReviewCommentAt,
        LastSyncedAt = binding.State.LastSyncedAt,
        CreatedAt = binding.CreatedAt,
        UpdatedAt = binding.State.UpdatedAt,
        SuppressMergeAutoClose = binding.State.SuppressMergeAutoClose,
    };

    public static IntentRepositoryBinding ToDomain(IntentRepositoryBindingRow row) =>
        IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
            Id: new BindingId(row.Id),
            IntentId: new IntentId(row.IntentId),
            Coordinate: new RepoCoordinate(row.Provider, row.Owner, row.Repo, row.Host, row.ProjectId),
            DefaultBranch: row.DefaultBranch,
            WorkspacePath: row.WorkspacePath,
            CloneStatus: row.CloneStatus,
            CloneError: row.CloneError,
            PullRequestNumber: row.PullRequestNumber,
            PullRequestState: row.PullRequestState,
            ReviewCommentsEtag: row.ReviewCommentsEtag,
            LastSeenReviewCommentAt: row.LastSeenReviewCommentAt,
            LastSyncedAt: row.LastSyncedAt,
            CreatedAt: row.CreatedAt,
            UpdatedAt: row.UpdatedAt,
            SuppressMergeAutoClose: row.SuppressMergeAutoClose));
}
