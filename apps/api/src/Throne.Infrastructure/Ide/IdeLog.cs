using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Ide;

/// <summary>
/// LoggerMessage source-generator host for IDE openers. Same pattern as
/// <c>TerminalsLog</c> / <c>ProcessRunnerLog</c>: each emit site routes through
/// a generated partial so the call sites stay cold-path-friendly and satisfy
/// CA1848.
/// </summary>
internal static partial class IdeLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "IDE opener '{Provider}' exit {ExitCode} for {Path}: {Stderr}")]
    public static partial void OpenFailed(ILogger logger, string provider, int exitCode, string path, string stderr);
}
