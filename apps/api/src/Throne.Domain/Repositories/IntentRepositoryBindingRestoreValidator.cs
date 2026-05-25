namespace Throne.Domain.Repositories;

/// <summary>
/// Re-validates a snapshot before <see cref="IntentRepositoryBindingFactory.Restore"/> hands
/// it to the aggregate constructor. Catches tampered persistence documents (unknown enum-like
/// strings, PR-state without PR-number).
/// </summary>
internal static class IntentRepositoryBindingRestoreValidator
{
    public static void EnsureValid(IntentRepositoryBindingSnapshot snapshot)
    {
        IntentRepositoryBindingInputs.EnsureCommonInputs(
            snapshot.Coordinate, snapshot.DefaultBranch, snapshot.WorkspacePath);
        EnsureKnownCloneStatus(snapshot.CloneStatus);
        IntentRepositoryBindingInputs.EnsurePositivePullRequestNumber(snapshot.PullRequestNumber);
        EnsureKnownPullRequestState(snapshot.PullRequestState);
        EnsurePullRequestStateRequiresNumber(snapshot.PullRequestNumber, snapshot.PullRequestState);
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
}
