using Microsoft.Extensions.Logging;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamConnectLoop(UpstreamConnection connection, ILogger log)
{
    public async Task ConnectWithBackoffAsync(int maxAttempts, CancellationToken ct)
    {
        var connectedAttempt = await ReconnectWithBackoffAsync(maxAttempts, ct);
        StdioProxyLog.UpstreamConnected(log, connectedAttempt);
    }

    private async Task<int> ReconnectWithBackoffAsync(int maxAttempts, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await connection.ReconnectUnsafeAsync(ct);
                return attempt;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                var delay = UpstreamBackoff.Compute(attempt);
                StdioProxyLog.UpstreamConnectAttemptFailed(log, attempt, maxAttempts, delay, ex);
                await Task.Delay(delay, ct);
            }
        }

        await connection.ReconnectUnsafeAsync(ct);
        return maxAttempts;
    }
}
