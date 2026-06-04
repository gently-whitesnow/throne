using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Bridges one WebSocket connection ↔ one live tmux session via the CLI alone — no PTY
/// in the host process. Slice 2 ADR-0026 § 3 picks this path over Pty.Net so the infra
/// adds zero new NuGet deps; tmux already owns the real PTY around <c>claude</c>.
///
/// Sub-pipeline:
/// <list type="bullet">
///   <item><see cref="TerminalSessionAttach"/> — FIFO + <c>pipe-pane</c> + initial scrollback.</item>
///   <item><see cref="TerminalInboundPump"/> — WS → <c>send-keys</c> / <c>refresh-client</c>.</item>
///   <item><see cref="TerminalOutboundPump"/> — FIFO tail → WS <c>output</c> frames.</item>
///   <item>Liveness watcher — periodic <c>has-session</c> probe; tmux session exit triggers
///         a normal-closure shutdown.</item>
/// </list>
/// </summary>
internal sealed class TmuxStreamBridge(
    TmuxCli tmux,
    ITmuxSessionManager sessions,
    IOptions<TmuxOptions> options,
    ILogger<TmuxStreamBridge> log) : ITerminalStreamBridge
{
    private static readonly TimeSpan SessionPollInterval = TimeSpan.FromSeconds(2);

    public async Task RunAsync(string intentId, ITerminalSocket connection, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(connection);

        var sessionName = TmuxSessionName.For(intentId);
        if (!await sessions.HasSessionAsync(intentId, ct))
        {
            await connection.CloseAsync(WebSocketCloseCodes.PolicyViolation, "no_session", ct);
            return;
        }

        var attach = new TerminalSessionAttach(tmux, options, log);
        await using var fifo = await attach.AttachAsync(intentId, sessionName, connection, ct);
        if (fifo is null)
        {
            return;
        }

        await PumpAsync(intentId, sessionName, fifo.Path, connection, ct);
    }

    private async Task PumpAsync(
        string intentId,
        string sessionName,
        string fifoPath,
        ITerminalSocket connection,
        CancellationToken ct)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = linkedCts.Token;

        var inboundPump = new TerminalInboundPump(tmux);
        var inboundTask = inboundPump.RunAsync(connection, sessionName, token);
        var outboundTask = TerminalOutboundPump.RunAsync(connection, fifoPath, token);
        var aliveTask = WatchSessionAsync(intentId, token);

        var finished = await Task.WhenAny(inboundTask, outboundTask, aliveTask);
        await linkedCts.CancelAsync();

        var reason = TerminalBridgeShutdown.Classify(finished, inboundTask, outboundTask, aliveTask);
        await StopPipeAsync(sessionName);
        await connection.CloseAsync(reason.CloseCode, reason.Reason, CancellationToken.None);

        TerminalsLog.BridgeDetached(log, sessionName, reason.CloseCode, reason.Reason);
        await TerminalBridgeShutdown.SilenceAsync(inboundTask);
        await TerminalBridgeShutdown.SilenceAsync(outboundTask);
        await TerminalBridgeShutdown.SilenceAsync(aliveTask);
    }

    private async Task WatchSessionAsync(string intentId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(SessionPollInterval, ct);
            if (!await sessions.HasSessionAsync(intentId, CancellationToken.None))
            {
                return;
            }
        }
    }

    private async Task StopPipeAsync(string sessionName)
    {
        try
        {
            await tmux.RunAsync(["pipe-pane", "-t", sessionName], CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // shutdown — ignore
        }
    }
}

/// <summary>
/// Close codes from the yaml contract surface. Public so tests outside the assembly can
/// assert on them without re-declaring the constants.
/// </summary>
internal static class WebSocketCloseCodes
{
    public const int NormalClosure = 1000;
    public const int PolicyViolation = 1008;
    public const int InternalError = 1011;
}
