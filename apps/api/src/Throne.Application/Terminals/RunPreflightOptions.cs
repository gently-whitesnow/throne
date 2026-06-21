namespace Throne.Application.Terminals;

/// <summary>
/// Configuration for the Slice 2 Run pre-flight pipeline. Bound from the
/// <c>Throne:Run</c> section in <c>appsettings*.json</c>.
/// </summary>
public sealed class RunPreflightOptions
{
    public const string SectionName = "Throne:Run";

    public string ApiBaseUrl { get; set; } = SessionHookOptions.DefaultApiBaseUrl;

    /// <summary>
    /// Maximum number of seconds the pre-flight will block while all bindings
    /// transition to <c>clone_status=ready</c>. Default 300s (5 minutes) per the
    /// parent intent — long enough for a couple of medium repos to clone over slow
    /// networks, short enough to fail loudly rather than hang the HTTP request.
    /// </summary>
    public int CloneWaitSeconds { get; set; } = 300;

    /// <summary>
    /// Polling interval (milliseconds) used while waiting for clones to finish.
    /// Small enough to feel responsive in the UI, large enough not to hammer Mongo.
    /// </summary>
    public int PollIntervalMilliseconds { get; set; } = 500;

    /// <summary>
    /// Upper bound (milliseconds) for the post-spawn wait that gates user-prompt delivery on
    /// vendor TUI readiness — see <see cref="TmuxTuiReadinessWaiter"/>. Claude Code on this
    /// box cold-starts in 1200–1500 ms; 5000 ms leaves headroom for slower machines without
    /// hanging the Run pre-flight indefinitely on a stuck TUI.
    /// </summary>
    public int TuiReadinessTimeoutMilliseconds { get; set; } = 5000;

    /// <summary>
    /// Poll interval (milliseconds) for the TUI readiness wait. <c>tmux capture-pane</c> is a
    /// cheap shell-out so polling at 100 ms gives sub-second detection without flooding tmux.
    /// </summary>
    public int TuiReadinessPollIntervalMilliseconds { get; set; } = 100;

    /// <summary>
    /// Hostname for the shared persistent <c>opencode serve</c> process. One serve per Throne
    /// host runs in a fixed-name tmux session; every OpenCode session is created against it and
    /// the operator's <c>opencode attach</c> front connects to it. Loopback by default.
    /// </summary>
    public string OpencodeServeHostname { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port for the shared <c>opencode serve</c>. Fixed (not per-intent) so the address stays
    /// known across a Throne restart and the already-running serve tmux session is always
    /// reachable. Default 4096 = OpenCode's own default serve port.
    /// </summary>
    public int OpencodeServePort { get; set; } = 4096;

    /// <summary>
    /// Upper bound (milliseconds) to wait for a freshly spawned <c>opencode serve</c> to answer
    /// <c>GET /global/health</c>. Cold start is ~1–2 s on this box; 10 000 ms covers slower
    /// machines without hanging the Run pre-flight on a serve that never comes up.
    /// </summary>
    public int OpencodeServeReadinessTimeoutMilliseconds { get; set; } = 10000;
}
