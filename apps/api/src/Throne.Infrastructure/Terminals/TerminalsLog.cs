using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// LoggerMessage source-generator host for the Terminals module. Mirrors
/// <c>ProcessRunnerLog</c> in the Git module — every emit site goes through a
/// generated partial to keep the call sites cold-path-friendly.
/// </summary>
internal static partial class TerminalsLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "tmux binary unavailable for {Operation}: {Detail}")]
    public static partial void TmuxMissing(ILogger logger, string operation, string detail);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "tmux spawn failed for {SessionName} (exit={ExitCode}): {Detail}")]
    public static partial void TmuxSpawnFailed(ILogger logger, string sessionName, int exitCode, string detail);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "terminal bridge attached to {SessionName}; fifo={FifoPath}")]
    public static partial void BridgeAttached(ILogger logger, string sessionName, string fifoPath);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "terminal bridge detached from {SessionName} (close_code={CloseCode}, reason={Reason})")]
    public static partial void BridgeDetached(ILogger logger, string sessionName, int closeCode, string reason);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "terminal bridge for {SessionName} failed: {Reason}")]
    public static partial void BridgeFailed(ILogger logger, string sessionName, string reason);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "capability probe '{Name}': detected={Detected} detail={Detail}")]
    public static partial void CapabilityProbed(ILogger logger, string name, bool detected, string? detail);
}
