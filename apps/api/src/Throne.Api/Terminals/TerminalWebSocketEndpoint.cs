using Microsoft.Extensions.Logging;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Api.Terminals;

/// <summary>
/// Minimal-API handler for <c>GET /api/v1/intents/{intent_id}/terminal/ws</c>. The
/// endpoint is attach-only (Slice 2: spawn lives in T-05 / pre-flight pipeline). If
/// the tmux prerequisite is not detected on this host the upgrade is rejected with
/// HTTP 422 before the WebSocket handshake completes — matches the OpenAPI surface
/// used by the Run REST endpoints.
/// </summary>
public sealed class TerminalWebSocketEndpoint(
    ITerminalStreamBridge bridge,
    ICapabilityDetectionCache detection,
    ILogger<TerminalWebSocketEndpoint> log)
{
    public async Task HandleAsync(HttpContext context, string intentId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(intentId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!await IsTmuxDetectedAsync(context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var adapter = new TerminalWebSocketAdapter(socket);

        try
        {
            await bridge.RunAsync(intentId, adapter, context.RequestAborted);
        }
        catch (Exception ex)
        {
            TerminalEndpointLog.BridgeAborted(log, intentId, ex);
            await adapter.CloseAsync(1011, "internal_error", CancellationToken.None);
        }
    }

    private async Task<bool> IsTmuxDetectedAsync(CancellationToken ct)
    {
        var probe = await detection.GetAsync("tmux", ct);
        return probe is { Detected: true };
    }
}
