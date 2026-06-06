using Microsoft.Extensions.Logging;

namespace Throne.Mcp.Stdio;

internal static partial class StdioProxyLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Throne MCP STDIO proxy connected to upstream on attempt {Attempt}.")]
    public static partial void UpstreamConnected(ILogger logger, int attempt);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Upstream connect attempt {Attempt}/{MaxAttempts} failed; retrying in {Delay}.")]
    public static partial void UpstreamConnectAttemptFailed(
        ILogger logger, int attempt, int maxAttempts, TimeSpan delay, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Throne MCP STDIO proxy could not connect to upstream Throne API at {Url}. Is Throne.Api running?")]
    public static partial void UpstreamConnectFailed(ILogger logger, Uri url, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Throne MCP STDIO proxy ready. Upstream: {Url}. Tools forwarded: {Count}.")]
    public static partial void ProxyReady(ILogger logger, Uri url, int count);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Upstream session ended; reconnecting.")]
    public static partial void UpstreamDisconnected(ILogger logger);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Upstream reconnected. Tools forwarded: {Count}.")]
    public static partial void UpstreamReconnected(ILogger logger, int count);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning,
        Message = "Proactive upstream reconnect failed; retrying with backoff.")]
    public static partial void UpstreamProactiveReconnectFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning,
        Message = "Upstream call '{Tool}' hit a transport fault; retrying after reconnect.")]
    public static partial void UpstreamCallRetrying(ILogger logger, string tool, Exception exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug,
        Message = "Disposing previous upstream client failed.")]
    public static partial void UpstreamDisposeFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information,
        Message = "Upstream tool snapshot changed; new tool count: {Count}.")]
    public static partial void UpstreamToolsChanged(ILogger logger, int count);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning,
        Message = "Upstream ServerInstructions diverged from initial value; proxy will exit so the host restarts it.")]
    public static partial void UpstreamInstructionsDiverged(ILogger logger);

    [LoggerMessage(EventId = 12, Level = LogLevel.Information,
        Message = "Forwarded {Method} to downstream client.")]
    public static partial void DownstreamToolListChanged(ILogger logger, string method = "notifications/tools/list_changed");

    [LoggerMessage(EventId = 13, Level = LogLevel.Warning,
        Message = "Failed to forward {Method} downstream.")]
    public static partial void DownstreamNotificationFailed(ILogger logger, string method, Exception exception);

    [LoggerMessage(EventId = 14, Level = LogLevel.Warning,
        Message = "Upstream ServerInstructions changed after redeploy; exiting (code 2) so host can restart the proxy.")]
    public static partial void DownstreamInstructionsDivergedExit(ILogger logger);
}
