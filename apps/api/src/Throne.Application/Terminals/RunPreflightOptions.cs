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
}
