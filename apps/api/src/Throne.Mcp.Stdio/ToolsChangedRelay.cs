using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Throne.Mcp.Stdio;

/// <summary>
/// Subscribes to <see cref="ResilientUpstreamClient.ToolsChanged"/> and re-emits
/// <c>notifications/tools/list_changed</c> downstream so Claude Code (or any other MCP host)
/// re-reads the tool list after an upstream redeploy.
///
/// Also listens for <see cref="ResilientUpstreamClient.ServerInstructionsDiverged"/>: when the
/// upstream's <c>ServerInstructions</c> changes, the downstream server can't refresh them in
/// place — they are read once at initialize. We exit the process and rely on the host to
/// restart us; on the next launch the downstream will re-initialize with the new instructions.
/// </summary>
internal sealed class ToolsChangedRelay : BackgroundService
{
    private readonly UpstreamToolsState _tools;
    private readonly UpstreamInstructionsState _instructions;
    private readonly McpServer _server;
    private readonly ILogger<ToolsChangedRelay> _log;

    public ToolsChangedRelay(
        UpstreamToolsState tools,
        UpstreamInstructionsState instructions,
        McpServer server,
        ILogger<ToolsChangedRelay> log)
    {
        _tools = tools;
        _instructions = instructions;
        _server = server;
        _log = log;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _tools.ToolsChanged += OnToolsChanged;
        _instructions.ServerInstructionsDiverged += OnInstructionsDiverged;

        stoppingToken.Register(() =>
        {
            _tools.ToolsChanged -= OnToolsChanged;
            _instructions.ServerInstructionsDiverged -= OnInstructionsDiverged;
        });

        return Task.CompletedTask;
    }

    private void OnToolsChanged()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _server.SendNotificationAsync(
                    NotificationMethods.ToolListChangedNotification,
                    new ToolListChangedNotificationParams());
                StdioProxyLog.DownstreamToolListChanged(_log);
            }
            catch (Exception ex)
            {
                StdioProxyLog.DownstreamNotificationFailed(_log, NotificationMethods.ToolListChangedNotification, ex);
            }
        });
    }

    private void OnInstructionsDiverged()
    {
        StdioProxyLog.DownstreamInstructionsDivergedExit(_log);
        // Exit asynchronously so the current reconnect call can unwind cleanly.
        _ = Task.Run(static () => Environment.Exit(2));
    }
}
