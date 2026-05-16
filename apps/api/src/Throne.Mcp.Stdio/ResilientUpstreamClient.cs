using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Throne.Mcp.Stdio;

/// <summary>
/// Wrapper around <see cref="McpClient"/> that survives upstream redeploys.
/// </summary>
public sealed class ResilientUpstreamClient : IAsyncDisposable
{
    private readonly UpstreamConnection _connection;
    private readonly UpstreamConnectLoop _connectLoop;
    private readonly UpstreamToolCaller _toolCaller;

    public ResilientUpstreamClient(
        Func<CancellationToken, Task<McpClient>> connectAsync,
        ILogger<ResilientUpstreamClient> log)
    {
        _connection = new UpstreamConnection(connectAsync, log);
        _connectLoop = new UpstreamConnectLoop(_connection, log);
        _toolCaller = new UpstreamToolCaller(_connection, log);
    }

    public IReadOnlyList<Tool> CurrentTools => _connection.ToolsState.CurrentTools;
    public string? InitialServerInstructions => _connection.InstructionsState.InitialServerInstructions;

    internal UpstreamConnection Connection => _connection;

    public Task ConnectWithBackoffAsync(int maxAttempts, CancellationToken ct) =>
        _connectLoop.ConnectWithBackoffAsync(maxAttempts, ct);

    public ValueTask<CallToolResult> CallToolAsync(CallToolRequestParams request, CancellationToken ct) =>
        _toolCaller.CallToolAsync(request, ct);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
