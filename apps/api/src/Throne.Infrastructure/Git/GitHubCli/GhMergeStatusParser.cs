using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses <c>gh pr view --json mergeable,mergeStateStatus,statusCheckRollup,url</c>
/// into a provider-neutral <see cref="PullRequestMergeStatus"/>. GitHub computes
/// <c>mergeable</c> asynchronously, so <c>UNKNOWN</c> is mapped to
/// <see cref="PullRequestMergeability.Checking"/> rather than a hard failure.
/// </summary>
internal static class GhMergeStatusParser
{
    public static PullRequestMergeStatus Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var mergeable = GhJson.String(root, "mergeable");
        var stateStatus = GhJson.String(root, "mergeStateStatus");
        return new PullRequestMergeStatus(
            Mergeability: MapMergeability(mergeable, stateStatus),
            Checks: MapChecks(root),
            HtmlUrl: GhJson.String(root, "url"));
    }

    private static PullRequestMergeability MapMergeability(string? mergeable, string? stateStatus)
    {
        if (Eq(mergeable, "CONFLICTING") || Eq(stateStatus, "DIRTY"))
        {
            return PullRequestMergeability.Conflicting;
        }
        if (Eq(stateStatus, "BEHIND"))
        {
            return PullRequestMergeability.Behind;
        }
        if (Eq(stateStatus, "BLOCKED") || Eq(stateStatus, "DRAFT"))
        {
            return PullRequestMergeability.Blocked;
        }
        if (Eq(mergeable, "UNKNOWN") || Eq(stateStatus, "UNKNOWN") || string.IsNullOrEmpty(mergeable))
        {
            return PullRequestMergeability.Checking;
        }
        if (Eq(mergeable, "MERGEABLE"))
        {
            // CLEAN / HAS_HOOKS / UNSTABLE are all mergeable; UNSTABLE just means
            // non-required checks are red, which the checks indicator reports separately.
            return PullRequestMergeability.Mergeable;
        }
        return PullRequestMergeability.Unknown;
    }

    private static PullRequestChecksState MapChecks(JsonElement root)
    {
        if (!root.TryGetProperty("statusCheckRollup", out var rollup) || rollup.ValueKind != JsonValueKind.Array)
        {
            return PullRequestChecksState.None;
        }
        var count = 0;
        var anyFailing = false;
        var anyPending = false;
        foreach (var item in rollup.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            count++;
            switch (Classify(item))
            {
                case PullRequestChecksState.Failing:
                    anyFailing = true;
                    break;
                case PullRequestChecksState.Pending:
                    anyPending = true;
                    break;
            }
        }
        if (count == 0)
        {
            return PullRequestChecksState.None;
        }
        if (anyFailing)
        {
            return PullRequestChecksState.Failing;
        }
        return anyPending ? PullRequestChecksState.Pending : PullRequestChecksState.Passing;
    }

    // CheckRun items carry status (QUEUED/IN_PROGRESS/COMPLETED) + conclusion;
    // StatusContext items carry a single state (SUCCESS/PENDING/FAILURE/ERROR).
    private static PullRequestChecksState Classify(JsonElement item)
    {
        var conclusion = GhJson.String(item, "conclusion");
        if (!string.IsNullOrEmpty(conclusion))
        {
            if (IsAny(conclusion, "FAILURE", "CANCELLED", "TIMED_OUT", "ACTION_REQUIRED", "STARTUP_FAILURE", "STALE"))
            {
                return PullRequestChecksState.Failing;
            }
            var status = GhJson.String(item, "status");
            return Eq(status, "COMPLETED") ? PullRequestChecksState.Passing : PullRequestChecksState.Pending;
        }
        var state = GhJson.String(item, "state");
        if (IsAny(state, "FAILURE", "ERROR"))
        {
            return PullRequestChecksState.Failing;
        }
        if (IsAny(state, "PENDING", "EXPECTED"))
        {
            return PullRequestChecksState.Pending;
        }
        return Eq(state, "SUCCESS") ? PullRequestChecksState.Passing : PullRequestChecksState.Pending;
    }

    private static bool Eq(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsAny(string? value, params string[] options)
    {
        foreach (var option in options)
        {
            if (Eq(value, option))
            {
                return true;
            }
        }
        return false;
    }
}
