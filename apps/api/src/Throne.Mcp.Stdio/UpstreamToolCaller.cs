using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamToolCaller(UpstreamConnection connection, ILogger log)
{
    public async ValueTask<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken ct)
    {
        var mayRetry = UpstreamToolRetryPolicy.CanRetry(request.Name, connection.ToolsState.CurrentTools);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var client = await connection.GetOrReconnectAsync(ct);
            try
            {
                return await client.CallToolAsync(request, ct);
            }
            catch (Exception ex) when (attempt == 0 && mayRetry && IsTransportFault(ex))
            {
                StdioProxyLog.UpstreamCallRetrying(log, request.Name, ex);
                await connection.ForceReconnectAsync(ct);
            }
            catch (Exception ex) when (attempt == 0 && IsTransportFault(ex))
            {
                await TryHealAsync(ct);
                throw;
            }
        }
        throw new InvalidOperationException("UpstreamToolCaller retry loop fell through.");
    }

    private async Task TryHealAsync(CancellationToken ct)
    {
        try
        {
            await connection.ForceReconnectAsync(ct);
        }
        catch (Exception ex)
        {
            StdioProxyLog.UpstreamProactiveReconnectFailed(log, ex);
        }
    }

    private static bool IsTransportFault(Exception ex) =>
        ex is ClientTransportClosedException
            or HttpRequestException
            or IOException
            or ObjectDisposedException;
}
