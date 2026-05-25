using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Git;

/// <summary>
/// LoggerMessage source-generator host for <see cref="ProcessRunner"/>.
/// </summary>
internal static partial class ProcessRunnerLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "ProcessRunner: starting {FileName} (args_count={ArgumentCount}, cwd={WorkingDirectory}).")]
    public static partial void Starting(ILogger logger, string fileName, int argumentCount, string? workingDirectory);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "ProcessRunner: finished {FileName} (exit={ExitCode}, elapsed_ms={ElapsedMs}).")]
    public static partial void Finished(ILogger logger, string fileName, int exitCode, long elapsedMs);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "ProcessRunner: timeout after {ElapsedMs}ms for {FileName}; killed process tree.")]
    public static partial void Timeout(ILogger logger, string fileName, long elapsedMs);
}
