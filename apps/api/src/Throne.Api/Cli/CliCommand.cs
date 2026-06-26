namespace Throne.Api.Cli;

internal enum CliCommand
{
    /// <summary>Bare <c>throne</c> (detached) or <c>throne -a</c> (foreground).</summary>
    Start,
    Stop,
    Restart,
    Status,
    Logs,
    Update,

    /// <summary>Raw Kestrel host entry — the detached child and power users.</summary>
    Serve,
    Version,
    Help,
}
