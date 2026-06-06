using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Routes spawn outcomes to the LoggerMessage source-generated emitters
/// (<see cref="TerminalsLog"/>), on behalf of <see cref="TmuxSessionManager"/>.
/// </summary>
internal static class SpawnOutcomeReporter
{
    public static void Report(ILogger log, string sessionName, TmuxRunOutcome outcome, string? detail)
    {
        if (!outcome.IsAvailable)
        {
            TerminalsLog.TmuxMissing(log, "spawn", outcome.BinaryMissingDetail ?? string.Empty);
            return;
        }
        if (!outcome.IsSuccess)
        {
            TerminalsLog.TmuxSpawnFailed(log, sessionName, outcome.Result?.ExitCode ?? -1, detail ?? string.Empty);
        }
    }
}
