namespace Throne.Application.Terminals;

/// <summary>
/// Bridges a single accepted WebSocket connection to a live tmux session. Slice 2 ADR-0026 § 3
/// fixes the wire contract — see <c>specs/contracts/realtime/websocket/terminal.yaml</c>.
///
/// Implementations:
/// <list type="bullet">
///   <item>Push a one-shot scrollback dump (<c>tmux capture-pane</c>) as the first
///         <c>output</c> frame, so reconnects see prior context.</item>
///   <item>Convert inbound <c>input</c> frames into <c>tmux send-keys -H {hex}</c> calls
///         (raw byte passthrough including control codes).</item>
///   <item>Convert inbound <c>resize</c> frames into <c>tmux refresh-client -C {cols}x{rows}</c>.</item>
///   <item>Tail <c>tmux pipe-pane</c> output and forward chunks as <c>output</c> frames.</item>
/// </list>
/// </summary>
public interface ITerminalStreamBridge
{
    /// <summary>
    /// Take ownership of <paramref name="connection"/> until either the WebSocket
    /// closes, the tmux session disappears, or <paramref name="ct"/> fires. The
    /// connection is closed by the bridge itself before the task completes.
    /// </summary>
    Task RunAsync(string intentId, ITerminalSocket connection, CancellationToken ct);
}

/// <summary>
/// Adapter over <see cref="System.Net.WebSockets.WebSocket"/>. Application layer talks to
/// this interface so the bridge can be unit-tested against an in-memory pair.
/// </summary>
public interface ITerminalSocket
{
    /// <summary>
    /// Read the next inbound message (UTF-8 text frame per the yaml contract). Returns
    /// <c>null</c> when the peer closed cleanly. Throws when the connection aborts.
    /// </summary>
    Task<string?> ReceiveTextAsync(CancellationToken ct);

    /// <summary>
    /// Send a UTF-8 text frame.
    /// </summary>
    Task SendTextAsync(string payload, CancellationToken ct);

    /// <summary>
    /// Close the connection with one of the codes declared in the yaml contract
    /// (<c>1000</c> normal, <c>1008</c> policy, <c>1011</c> internal).
    /// </summary>
    Task CloseAsync(int closeCode, string? description, CancellationToken ct);
}
