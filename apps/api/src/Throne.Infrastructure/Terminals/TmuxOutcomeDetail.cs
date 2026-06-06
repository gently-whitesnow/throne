namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Folds <see cref="TmuxRunOutcome"/> into a single short detail string for surface
/// reporting (logs, capability detection, spawn results).
/// </summary>
internal static class TmuxOutcomeDetail
{
    public static string? Extract(TmuxRunOutcome outcome)
    {
        if (outcome.BinaryMissingDetail is { } missing)
        {
            return missing;
        }
        if (outcome.FailureDetail is { } failure)
        {
            return failure;
        }
        return LastStderrLine(outcome.Result?.StandardError);
    }

    private static string? LastStderrLine(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return null;
        }
        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? null : lines[^1];
    }
}
