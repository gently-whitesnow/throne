namespace Throne.Application.Terminals;

/// <summary>
/// Configuration for the Slice 2 Run pre-flight pipeline. Bound from the
/// <c>Throne:Run</c> section in <c>appsettings*.json</c>.
/// </summary>
public sealed class RunPreflightOptions
{
    public const string SectionName = "Throne:Run";

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
}
