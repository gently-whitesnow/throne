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

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning,
        Message = "failed to pre-seed {Vendor} trust for {WorkspacePath}: {Reason}")]
    public static partial void WorkspaceTrustSeedFailed(ILogger logger, string vendor, string workspacePath, string reason);

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning,
        Message = "failed to remove {Vendor} trust under {DirectoryPrefix}: {Reason}")]
    public static partial void WorkspaceUntrustFailed(ILogger logger, string vendor, string directoryPrefix, string reason);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information,
        Message = "tmux kill-session: name={SessionName} pre_alive={PreAlive} exit={ExitCode} elapsed_ms={ElapsedMs} stderr={Stderr} post_alive={PostAlive}")]
    public static partial void TmuxKillResult(
        ILogger logger,
        string sessionName,
        bool preAlive,
        int exitCode,
        long elapsedMs,
        string stderr,
        bool postAlive);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning,
        Message = "tmux kill-session: name={SessionName} did not start (binary missing or launcher failure): {Detail}")]
    public static partial void TmuxKillUnavailable(ILogger logger, string sessionName, string detail);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information,
        Message = "OpenCode shared serve is healthy at {Endpoint}.")]
    public static partial void OpencodeServeReady(ILogger logger, Uri endpoint);

    [LoggerMessage(EventId = 15, Level = LogLevel.Debug,
        Message = "terminal input {SessionName}: chars={CharLength} bytes={ByteLength} control={ControlHex}")]
    public static partial void TerminalInputFrame(
        ILogger logger,
        string sessionName,
        int charLength,
        int byteLength,
        string controlHex);

    [LoggerMessage(EventId = 16, Level = LogLevel.Debug,
        Message = "terminal control input {SessionName}: chars={CharLength} bytes={ByteLength} control={ControlHex}")]
    public static partial void TerminalControlInputFrame(
        ILogger logger,
        string sessionName,
        int charLength,
        int byteLength,
        string controlHex);

    [LoggerMessage(EventId = 17, Level = LogLevel.Warning,
        Message = "terminal input send failed {SessionName}: chars={CharLength} bytes={ByteLength} control={ControlHex} detail={Detail}")]
    public static partial void TerminalInputSendFailed(
        ILogger logger,
        string sessionName,
        int charLength,
        int byteLength,
        string controlHex,
        string detail);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug,
        Message = "terminal input buffered {SessionName}: chars={CharLength} bytes={ByteLength} buffer={BufferName} success={Success}")]
    public static partial void TerminalInputBuffered(
        ILogger logger,
        string sessionName,
        int charLength,
        int byteLength,
        string bufferName,
        bool success);

    [LoggerMessage(EventId = 18, Level = LogLevel.Information,
        Message = "tmux prompt submit starting {SessionName}: buffer={BufferName} file_bytes={FileBytes} path={FilePath}")]
    public static partial void TmuxPromptSubmitStarting(
        ILogger logger,
        string sessionName,
        string bufferName,
        long fileBytes,
        string filePath);

    [LoggerMessage(EventId = 19, Level = LogLevel.Information,
        Message = "tmux prompt submit step {SessionName}: step={Step} success={Success} exit={ExitCode} detail={Detail}")]
    public static partial void TmuxPromptSubmitStep(
        ILogger logger,
        string sessionName,
        string step,
        bool success,
        int exitCode,
        string detail);
}
