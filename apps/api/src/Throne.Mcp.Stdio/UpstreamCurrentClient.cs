using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamCurrentClient(ILogger log) : IAsyncDisposable
{
    public McpClient? Current { get; private set; }

    public bool TryGetAlive(out McpClient client)
    {
        client = Current!;
        return Current is not null && !Current.Completion.IsCompleted;
    }

    public async Task ReplaceAsync(McpClient client)
    {
        await DisposeAsync();
        Current = client;
    }

    public bool IsCurrent(McpClient client) => ReferenceEquals(client, Current);

    public async ValueTask DisposeAsync()
    {
        if (Current is null)
        {
            return;
        }
        try { await Current.DisposeAsync(); }
        catch (Exception ex) { StdioProxyLog.UpstreamDisposeFailed(log, ex); }
        Current = null;
    }
}
