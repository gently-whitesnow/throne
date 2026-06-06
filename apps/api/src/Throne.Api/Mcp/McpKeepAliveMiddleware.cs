using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Throne.Api.Mcp;

/// <summary>
/// Periodically writes an SSE keep-alive comment frame onto any long-lived <c>/mcp</c>
/// streamable-HTTP response (the GET listening stream and the SSE-bodied POST responses)
/// so intermediate proxies and the client's HTTP layer (e.g. mcp-remote / undici on
/// Node.js, which raises <c>UND_ERR_HEADERS_TIMEOUT</c> after ~5 min of idle) do not abort the
/// long-lived connection. Mirrors the keep-alive pattern in
/// <see cref="Realtime.RealtimeController"/>; ModelContextProtocol.AspNetCore 0.3.0-preview.4
/// does not yet expose its own heartbeat option for the streamable HTTP transport.
/// </summary>
public sealed partial class McpKeepAliveMiddleware
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);
    private static readonly byte[] KeepAliveFrame = Encoding.UTF8.GetBytes(": keep-alive\n\n");

    private readonly RequestDelegate _next;
    private readonly ILogger<McpKeepAliveMiddleware> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public McpKeepAliveMiddleware(
        RequestDelegate next,
        ILogger<McpKeepAliveMiddleware> logger,
        IHostApplicationLifetime lifetime)
    {
        _next = next;
        _logger = logger;
        _lifetime = lifetime;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Cover every /mcp request, not just GET: streamable-HTTP keeps the server→client
        // SSE stream open on POST responses too, so a method gate would leave those hanging.
        if (!context.Request.Path.Equals("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // The MCP SDK's streamable-HTTP handler blocks inside `_next` for the life of the
        // connection and only unwinds on RequestAborted. Kestrel does not abort active
        // requests on graceful shutdown until the host's ShutdownTimeout elapses, so without
        // this the process would hang for the full timeout per open MCP stream. Aborting the
        // connection on ApplicationStopping makes shutdown prompt.
        using var stopReg = _lifetime.ApplicationStopping.Register(
            static state => ((HttpContext)state!).Abort(), context);

        var gate = new SemaphoreSlim(1, 1);
        var originalBody = context.Response.Body;
        var serialized = new SerializingWriteStream(originalBody, gate);
        context.Response.Body = serialized;

        using var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        Task? keepAliveTask = null;

        context.Response.OnStarting(() =>
        {
            var contentType = context.Response.ContentType;
            if (contentType is not null &&
                contentType.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase))
            {
                keepAliveTask = RunKeepAliveAsync(serialized, keepAliveCts.Token);
            }
            return Task.CompletedTask;
        });

        try
        {
            await _next(context);
        }
        finally
        {
            keepAliveCts.Cancel();
            if (keepAliveTask is not null)
            {
                try { await keepAliveTask; }
                catch { /* surfaced via logger inside the loop */ }
            }
            context.Response.Body = originalBody;
        }
    }

    private async Task RunKeepAliveAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(KeepAliveInterval, ct);
                await stream.WriteAsync(KeepAliveFrame, ct);
                await stream.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            LogKeepAliveTerminated(_logger, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "MCP SSE keep-alive loop terminated.")]
    private static partial void LogKeepAliveTerminated(ILogger logger, Exception exception);
}

/// <summary>
/// Wraps an underlying response stream and serializes all writes through a <see cref="SemaphoreSlim"/>
/// so that the keep-alive loop and the MCP SDK's own writer cannot interleave bytes inside a single
/// SSE frame. The class deliberately overrides every Stream member; Stream itself does not provide a
/// usable thin-forwarder so each member is a one-line shim guarded by the same gate.
/// </summary>
internal sealed class SerializingWriteStream : Stream
{
    private readonly Stream _inner;
    private readonly SemaphoreSlim _gate;

    public SerializingWriteStream(Stream inner, SemaphoreSlim gate)
    {
        _inner = inner;
        _gate = gate;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        _gate.Wait();
        try { _inner.Flush(); }
        finally { _gate.Release(); }
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await _inner.FlushAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _gate.Wait();
        try { _inner.Write(buffer, offset, count); }
        finally { _gate.Release(); }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await _inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken); }
        finally { _gate.Release(); }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await _inner.WriteAsync(buffer, cancellationToken); }
        finally { _gate.Release(); }
    }
}
