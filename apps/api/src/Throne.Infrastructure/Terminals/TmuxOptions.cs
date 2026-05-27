namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Configuration for the tmux shell-out surface. Defaults keep the box small: the
/// system <c>tmux</c> binary, a generous spawn/has-session timeout and a per-bridge
/// FIFO scratch dir under <c>/tmp</c>.
/// </summary>
public sealed class TmuxOptions
{
    public const string SectionName = "Throne:Tmux";

    /// <summary>
    /// Path or PATH-resolvable name of the tmux executable. Override only when running
    /// inside a sandbox that ships tmux at a non-standard location.
    /// </summary>
    public string ExecutablePath { get; set; } = "tmux";

    /// <summary>
    /// Maximum wall-clock for any single tmux subcommand (`new`, `has-session`,
    /// `kill-session`, `ls`, `send-keys`, `capture-pane`, `pipe-pane`,
    /// `refresh-client`). 10s is comfortable — most calls return in single-digit ms.
    /// </summary>
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Directory holding the per-connection FIFO that <c>tmux pipe-pane</c> writes into.
    /// The bridge creates and deletes its own fifo under this root; the directory is
    /// created lazily on first use.
    /// </summary>
    public string PipeRootDirectory { get; set; } = "/tmp/throne-terminal-pipes";
}
