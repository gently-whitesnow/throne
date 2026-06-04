using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
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
    private readonly IHostApplicationLifetime _lifetime;

    public RealtimeController(IRealtimeEventBroker broker, IHostApplicationLifetime lifetime)
    {
        _broker = broker;
        _lifetime = lifetime;
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        Response.Headers["Content-Type"] = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        // Kestrel does not abort active requests on graceful shutdown until the host's
        // ShutdownTimeout elapses; without this the SSE loop would keep the process
        // alive for the full timeout. Cancelling on ApplicationStopping closes the
        // stream promptly so shutdown is fast.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.ApplicationStopping);
        var streamCt = linked.Token;

        using var subscription = _broker.Subscribe();

        var keepAlive = WriteKeepAliveLoopAsync(streamCt);
        try
        {
            await foreach (var envelope in subscription.ReadAllAsync(streamCt))
            {
                await WriteEventAsync(envelope, streamCt);
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnect or graceful shutdown — normal
        }
        finally
        {
            await keepAlive;
        }
    }

    private async Task WriteEventAsync(RealtimeEventEnvelope envelope, CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(envelope.Payload, PayloadOptions);
        var frame = $"event: {envelope.Name}\ndata: {payloadJson}\n\n";
        await Response.WriteAsync(frame, ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task WriteKeepAliveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(KeepAliveInterval, ct);
                await Response.WriteAsync(": keep-alive\n\n", ct);
                await Response.Body.FlushAsync(ct);
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
