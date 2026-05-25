using Throne.Domain.Intents;

namespace Throne.Domain.Repositories;

public static class IntentRepositoryBindingFactory
{
    /// <summary>
    /// Create a fresh binding in <see cref="CloneStatusNames.Pending"/>. PR is optional and,
    /// when supplied, is attached eagerly but with <c>pull_request_state = null</c> — the
    /// PR-sync background service in T-10 records the upstream state on the first probe.
    /// </summary>
    public static IntentRepositoryBinding Create(
        BindingId id,
        IntentId intentId,
        RepoCoordinate coordinate,
        string defaultBranch,
        string workspacePath,
        int? pullRequestNumber,
        DateTimeOffset now)
    {
        IntentRepositoryBindingInputs.EnsureCommonInputs(coordinate, defaultBranch, workspacePath);
        IntentRepositoryBindingInputs.EnsurePositivePullRequestNumber(pullRequestNumber);

        var state = new IntentRepositoryBindingState(
            DefaultBranch: defaultBranch,
            CloneStatus: CloneStatusNames.Pending,
            CloneError: null,
            PullRequestNumber: pullRequestNumber,
            PullRequestState: null,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            UpdatedAt: now);

        return new IntentRepositoryBinding(id, intentId, coordinate, workspacePath, now, state);
    }

    /// <summary>
    /// Rehydrate a binding from persistence (T-04). All invariants on enum-like fields are
    /// re-checked so a tampered document fails fast.
    /// </summary>
    public static IntentRepositoryBinding Restore(IntentRepositoryBindingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        IntentRepositoryBindingRestoreValidator.EnsureValid(snapshot);

        var state = new IntentRepositoryBindingState(
            DefaultBranch: snapshot.DefaultBranch,
            CloneStatus: snapshot.CloneStatus,
            CloneError: snapshot.CloneError,
            PullRequestNumber: snapshot.PullRequestNumber,
            PullRequestState: snapshot.PullRequestState,
            ReviewCommentsEtag: snapshot.ReviewCommentsEtag,
            LastSeenReviewCommentAt: snapshot.LastSeenReviewCommentAt,
            LastSyncedAt: snapshot.LastSyncedAt,
            UpdatedAt: snapshot.UpdatedAt);

        return new IntentRepositoryBinding(
            snapshot.Id, snapshot.IntentId, snapshot.Coordinate, snapshot.WorkspacePath,
            snapshot.CreatedAt, state);
    }
}
