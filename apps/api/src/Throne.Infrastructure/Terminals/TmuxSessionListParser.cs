using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Filters the raw newline-separated <c>tmux list-sessions -F '#S'</c> output down to
/// the throne-owned sessions. Kept apart from <see cref="TmuxSessionManager"/> so the
/// manager type stays under the per-type cyclomatic budget.
/// </summary>
internal static class TmuxSessionListParser
{
    public static string[] ParseThroneSessions(TmuxRunOutcome outcome)
    {
        if (!outcome.IsSuccess || outcome.Result is null)
        {
            return [];
        }
        return ExtractThroneLines(outcome.Result.StandardOutput);
    }

    private static string[] ExtractThroneLines(string stdout) =>
        stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsThroneSession)
            .ToArray();

    private static bool IsThroneSession(string line) =>
        line.StartsWith(TmuxSessionName.Prefix, StringComparison.Ordinal);
}
