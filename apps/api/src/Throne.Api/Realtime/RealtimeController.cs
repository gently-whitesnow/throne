using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Realtime.Contracts;

namespace Throne.Api.Realtime;

/// <summary>
/// Server-Sent Events stream of realtime events.
/// One subscription per HTTP connection; closes when the client disconnects.
///
/// Wire format: SSE frames with <c>event: &lt;name&gt;</c> + <c>data: &lt;json payload&gt;</c>.
/// See ADR-0008 (specs/contracts/realtime/events.yaml is the source of truth).
/// </summary>
[ApiController]
[Route("api/v1/realtime")]
public sealed class RealtimeController : ControllerBase
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    private readonly IRealtimeEventBroker _broker;

    internal RealtimeController(IRealtimeEventBroker broker)
    {
        _broker = broker;
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        using var subscription = _broker.Subscribe();

        var keepAlive = WriteKeepAliveLoopAsync(ct);
        try
        {
            await foreach (var envelope in subscription.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await WriteEventAsync(envelope, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnect — normal
        }
        finally
        {
            await keepAlive.ConfigureAwait(false);
        }
    }

    private async Task WriteEventAsync(RealtimeEventEnvelope envelope, CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(envelope.Payload, PayloadOptions);
        var frame = $"event: {envelope.Name}\ndata: {payloadJson}\n\n";
        await Response.WriteAsync(frame, ct).ConfigureAwait(false);
        await Response.Body.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task WriteKeepAliveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(KeepAliveInterval, ct).ConfigureAwait(false);
                await Response.WriteAsync(": keep-alive\n\n", ct).ConfigureAwait(false);
                await Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // expected on cancel
        }
        catch (ObjectDisposedException)
        {
            // response already closed
        }
    }
}
