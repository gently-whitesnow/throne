using Microsoft.Extensions.Logging;

namespace Throne.Mcp.Stdio;

internal static partial class StdioProxyLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Throne MCP STDIO proxy could not connect to upstream Throne API at {Url}. Is Throne.Api running?")]
    public static partial void UpstreamConnectFailed(ILogger logger, Uri url, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Connected to {Url} but failed to list tools.")]
    public static partial void UpstreamListToolsFailed(ILogger logger, Uri url, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Throne MCP STDIO proxy ready. Upstream: {Url}. Tools forwarded: {Count}.")]
    public static partial void ProxyReady(ILogger logger, Uri url, int count);
}
