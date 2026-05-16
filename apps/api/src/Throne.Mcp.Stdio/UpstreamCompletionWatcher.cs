using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Throne.Mcp.Stdio;

internal static class UpstreamCompletionWatcher
{
    public static async Task RunAsync(
        UpstreamConnection connection,
        McpClient client,
        ILogger log,
        CancellationToken stopToken)
    {
        if (!await WaitForCompletionAsync(client, stopToken) || !connection.IsCurrent(client))
        {
            return;
        }

        StdioProxyLog.UpstreamDisconnected(log);
        await ReconnectUntilStoppedAsync(connection, log, stopToken);
    }

    private static async Task<bool> WaitForCompletionAsync(McpClient client, CancellationToken stopToken)
    {
        try
        {
            await client.Completion.WaitAsync(stopToken);
            return !stopToken.IsCancellationRequested;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static async Task ReconnectUntilStoppedAsync(
        UpstreamConnection connection,
        ILogger log,
        CancellationToken stopToken)
    {
        for (var attempt = 1; !stopToken.IsCancellationRequested; attempt++)
        {
            if (await TryReconnectAsync(connection, log, stopToken))
            {
                return;
            }
            await Task.Delay(UpstreamBackoff.Compute(attempt), stopToken);
        }
    }

    private static async Task<bool> TryReconnectAsync(
        UpstreamConnection connection,
        ILogger log,
        CancellationToken stopToken)
    {
        try
        {
            await connection.ForceReconnectAsync(stopToken);
            StdioProxyLog.UpstreamReconnected(log, connection.ToolsState.CurrentTools.Count);
            return true;
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception ex)
        {
            StdioProxyLog.UpstreamProactiveReconnectFailed(log, ex);
            return false;
        }
    }
}
