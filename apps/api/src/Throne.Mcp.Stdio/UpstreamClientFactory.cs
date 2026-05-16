using ModelContextProtocol.Client;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamClientFactory(Func<CancellationToken, Task<McpClient>> connectAsync)
{
    public async Task<ConnectedUpstreamClient> ConnectAndListToolsAsync(CancellationToken ct)
    {
        var fresh = await connectAsync(ct);
        try
        {
            var tools = await fresh.ListToolsAsync(cancellationToken: ct);
            return new ConnectedUpstreamClient(fresh, tools);
        }
        catch
        {
            try { await fresh.DisposeAsync(); } catch { /* ignore */ }
            throw;
        }
    }
}
