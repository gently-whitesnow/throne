namespace Throne.Application.Terminals;

/// <summary>
/// Owns the tmux CLI surface needed by Slice 2 embedded-terminal: spawn, attach-existence
/// check, kill and list. The manager itself never holds session state — every call shells
/// out to <c>tmux</c> and returns the observed result. Per ADR-0026 § 2 the single source
/// of truth for liveness is <c>tmux has-session -t throne-{intent_id}</c>.
/// </summary>
public interface ITmuxSessionManager
{
    /// <summary>
    /// Idempotent spawn: <c>tmux new -ADs throne-{intent_id} -- {command} {args...}</c>.
    /// <list type="bullet">
    ///   <item>If the session already exists — no-op (the <c>-A</c> flag).</item>
    ///   <item>If <paramref name="command"/> exits, tmux removes the session (no bash wrapper —
    ///         see ADR-0026 § 2 / Slice 2 Q3).</item>
    /// </list>
    /// Returns <see cref="TmuxSpawnResult"/> describing the post-spawn observation
    /// (whether the session is now alive).
    /// </summary>
    Task<TmuxSpawnResult> SpawnAsync(TmuxSpawnRequest request, CancellationToken ct);

    /// <summary>
    /// <c>tmux has-session -t throne-{intent_id}</c>. Returns <c>true</c> when tmux reports
    /// the session alive (exit code 0). Any other observation (missing tmux binary,
    /// non-zero exit, network of stderr noise) collapses to <c>false</c> — callers treat
    /// the session as not running.
    /// </summary>
    Task<bool> HasSessionAsync(string intentId, CancellationToken ct);

    /// <summary>
    /// <c>tmux kill-session -t throne-{intent_id}</c>. Used by Restart-flow (T-05).
    /// Returns <c>true</c> if a session was killed, <c>false</c> if no such session existed.
    /// </summary>
    Task<bool> KillSessionAsync(string intentId, CancellationToken ct);

    /// <summary>
    /// <c>tmux ls -F '#S'</c>. Returns the set of currently-alive Throne sessions
    /// (filtered to the <see cref="TmuxSessionName.Prefix"/>). Empty when tmux is not
    /// running a server or the binary is missing.
    /// </summary>
    Task<IReadOnlyList<string>> ListThroneSessionsAsync(CancellationToken ct);

    /// <summary>
    /// <c>tmux send-keys -t throne-{intent_id} -l {text}</c>: pre-type literal text into the
    /// pane without submitting it (no Enter). Used by free mode so the boot-time claude prompt
    /// shows an editable starter phrase the operator finishes. Bytes buffered by the pane tty
    /// are drained by claude once its TUI starts reading stdin.
    /// </summary>
    Task SendLiteralTextAsync(string intentId, string text, CancellationToken ct);

    /// <summary>
    /// Pastes the contents of <paramref name="filePath"/> into the session's pane and submits
    /// it with Enter. The implementation chains
    /// <c>tmux load-buffer -b … &lt;path&gt;</c> + <c>tmux paste-buffer -d -p -b … -t …</c> +
    /// <c>tmux send-keys -t … Enter</c>. The buffer is loaded server-side from
    /// <paramref name="filePath"/> so multi-KB user prompts never travel on the spawn argv
    /// and never hit tmux's <c>MAX_IMSGSIZE</c> ceiling. <c>-p</c> wraps the payload in
    /// bracketed-paste markers so embedded newlines are not interpreted as submits by the
    /// vendor TUI; the final <c>send-keys Enter</c> is what actually submits the prompt to
    /// the agent so spawn → think latency stays the same as the old argv-positional path.
    /// </summary>
    Task PasteFileAsSubmittedPromptAsync(string intentId, string filePath, CancellationToken ct);
}

/// <summary>
/// Inputs for <see cref="ITmuxSessionManager.SpawnAsync"/>.
/// </summary>
/// <param name="IntentId">Intent id used to derive the session name via <see cref="TmuxSessionName.For"/>.</param>
/// <param name="WorkingDirectory">Absolute path tmux uses as the session cwd (intent workspace root).</param>
/// <param name="Command">Executable to run inside the tmux session (e.g. <c>claude</c>).</param>
/// <param name="Arguments">Pre-split argument vector for <paramref name="Command"/>.</param>
public sealed record TmuxSpawnRequest(
    string IntentId,
    string WorkingDirectory,
    string Command,
    IReadOnlyList<string> Arguments);

/// <summary>
/// Outcome of a spawn observation.
/// </summary>
/// <param name="SessionName">Computed <see cref="TmuxSessionName.For"/> for the intent.</param>
/// <param name="IsAlive">Whether <c>tmux has-session</c> confirmed the session after the spawn.</param>
/// <param name="Detail">Free-form note from tmux (stderr last line) — surfaced to the caller for logging.</param>
public sealed record TmuxSpawnResult(string SessionName, bool IsAlive, string? Detail);
