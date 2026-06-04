using Throne.Domain.Intents;

namespace Throne.Domain.Repositories;

/// <summary>
/// First-class aggregate (see ADR-0024) — NOT serialised into <c>Intent.text</c>
/// and NOT modelled as an <c>intent_links</c> edge (see ADR-0018).
///
/// Invariants:
/// <list type="bullet">
///   <item>The tuple <c>(IntentId, Provider, Owner, Repo)</c> is unique (enforced by repository).</item>
///   <item><see cref="WorkspacePath"/> is immutable after creation.</item>
///   <item>Status machine: <c>pending → cloning → ready | failed</c>; <c>ready → broken</c>
///         only via polling-observed 404, not from the bind flow.</item>
///   <item><see cref="AttachPullRequest"/> is valid only when no PR is attached yet.</item>
/// </list>
///
/// Per ADR-0025 the aggregate is rich-DDD: invariants and state transitions live
/// inside this class as <c>Create</c>/<c>Restore</c> factories and instance mutator
/// methods. The mutable portion is grouped into the
/// <see cref="IntentRepositoryBindingState"/> record so transitions are written as
/// <c>State = State with { … }</c>.
/// </summary>
public sealed class IntentRepositoryBinding
{
    private IntentRepositoryBinding(
        BindingId id,
        IntentId intentId,
        RepoCoordinate coordinate,
        string workspacePath,
        DateTimeOffset createdAt,
        IntentRepositoryBindingState state)
    {
        Id = id;
        IntentId = intentId;
        Coordinate = coordinate;
        WorkspacePath = workspacePath;
        CreatedAt = createdAt;
        State = state;
    }

    public BindingId Id { get; }
    public IntentId IntentId { get; }
    public RepoCoordinate Coordinate { get; }
    public string WorkspacePath { get; }
    public DateTimeOffset CreatedAt { get; }
    public IntentRepositoryBindingState State { get; private set; }

    /// <summary>
    /// Create a fresh binding in <see cref="CloneStatusNames.Pending"/>. PR is optional and,
    /// when supplied, is attached eagerly but with <c>pull_request_state = null</c> — the
    /// PR-sync background service records the upstream state on the first probe.
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
        EnsureCommonInputs(coordinate, defaultBranch, workspacePath);
        EnsurePositivePullRequestNumber(pullRequestNumber);

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
    /// Rehydrate a binding from persistence. All invariants on enum-like fields are
    /// re-checked so a tampered document fails fast.
    /// </summary>
    public static IntentRepositoryBinding Restore(IntentRepositoryBindingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureCommonInputs(snapshot.Coordinate, snapshot.DefaultBranch, snapshot.WorkspacePath);
        EnsureKnownCloneStatus(snapshot.CloneStatus);
        EnsurePositivePullRequestNumber(snapshot.PullRequestNumber);
        EnsureKnownPullRequestState(snapshot.PullRequestState);
        EnsurePullRequestStateRequiresNumber(snapshot.PullRequestNumber, snapshot.PullRequestState);

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

    public void MarkCloning(DateTimeOffset at)
    {
        EnsureTransition(State.CloneStatus, CloneStatusNames.Pending, CloneStatusNames.Cloning);
        State = State with
        {
            CloneStatus = CloneStatusNames.Cloning,
            CloneError = null,
            UpdatedAt = at,
        };
    }

    public void MarkPendingRetry(DateTimeOffset at)
    {
        EnsureTransition(State.CloneStatus, CloneStatusNames.Failed, CloneStatusNames.Pending);
        State = State with
        {
            CloneStatus = CloneStatusNames.Pending,
            CloneError = null,
            UpdatedAt = at,
        };
    }

    public void MarkReady(DateTimeOffset at)
    {
        EnsureTransition(State.CloneStatus, CloneStatusNames.Cloning, CloneStatusNames.Ready);
        State = State with
        {
            CloneStatus = CloneStatusNames.Ready,
            CloneError = null,
            UpdatedAt = at,
        };
    }

    public void MarkFailed(string error, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        EnsureFailedTransition(State.CloneStatus);
        State = State with
        {
            CloneStatus = CloneStatusNames.Failed,
            CloneError = error,
            UpdatedAt = at,
        };
    }

    public void MarkBroken(string reason, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureTransition(State.CloneStatus, CloneStatusNames.Ready, CloneStatusNames.Broken);
        State = State with
        {
            CloneStatus = CloneStatusNames.Broken,
            CloneError = reason,
            UpdatedAt = at,
        };
    }

    public void AttachPullRequest(int number, DateTimeOffset at)
    {
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Pull request number must be >= 1.");
        }

        if (State.PullRequestNumber is not null)
        {
            throw new InvalidOperationException(
                $"Pull request already attached to binding {Id}: #{State.PullRequestNumber}.");
        }

        State = State with { PullRequestNumber = number, UpdatedAt = at };
    }

    public void RecordPullRequestState(string state, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        if (State.PullRequestNumber is null)
        {
            throw new InvalidOperationException(
                $"Cannot record pull request state on binding {Id}: no PR attached.");
        }

        if (!PullRequestStateNames.IsKnown(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), $"Unknown pull request state: {state}.");
        }

        State = State with { PullRequestState = state, UpdatedAt = at };
    }

    public void RecordSync(string? etag, DateTimeOffset? lastSeenReviewCommentAt, DateTimeOffset at)
    {
        // Review D5 — sanitize through ReviewCommentsEtagNormalizer (RFC 7230
        // visible ASCII). Anything off-spec collapses to null so the next poll
        // does a full fetch instead of echoing an unsafe If-None-Match header.
        var prevCursor = State.LastSeenReviewCommentAt;
        var nextCursor = prevCursor is null
            ? lastSeenReviewCommentAt
            : lastSeenReviewCommentAt is null
                ? prevCursor
                : lastSeenReviewCommentAt > prevCursor ? lastSeenReviewCommentAt : prevCursor;
        State = State with
        {
            ReviewCommentsEtag = ReviewCommentsEtagNormalizer.Normalize(etag),
            LastSeenReviewCommentAt = nextCursor,
            LastSyncedAt = at,
            UpdatedAt = at,
        };
    }

    private static void EnsureCommonInputs(RepoCoordinate coordinate, string defaultBranch, string workspacePath)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultBranch);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
    }

    private static void EnsurePositivePullRequestNumber(int? number)
    {
        if (number is { } value && value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Pull request number must be >= 1.");
        }
    }

    private static void EnsureKnownCloneStatus(string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!CloneStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), $"Unknown clone_status: {status}.");
        }
    }

    private static void EnsureKnownPullRequestState(string? state)
    {
        if (state is not null && !PullRequestStateNames.IsKnown(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), $"Unknown pull_request_state: {state}.");
        }
    }

    private static void EnsurePullRequestStateRequiresNumber(int? number, string? state)
    {
        if (state is not null && number is null)
        {
            throw new ArgumentException(
                "pull_request_state must be null when no pull_request_number is attached.",
                nameof(state));
        }
    }

    private static void EnsureTransition(string current, string expectedFrom, string to)
    {
        if (!string.Equals(current, expectedFrom, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Illegal clone_status transition: {current} → {to}; expected from={expectedFrom}.");
        }
    }

    /// <summary>
    /// <c>failed</c> is reachable from either <c>pending</c> (interrupted at restart) or
    /// <c>cloning</c> (clone crashed) per ADR-0024 §5.
    /// </summary>
    private static void EnsureFailedTransition(string current)
    {
        if (current is CloneStatusNames.Pending or CloneStatusNames.Cloning)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Illegal clone_status transition: {current} → {CloneStatusNames.Failed}; "
            + $"expected from={CloneStatusNames.Pending} or {CloneStatusNames.Cloning}.");
    }
}
