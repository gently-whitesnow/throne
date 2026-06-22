namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral login probe for one embedded-terminal vendor: «is the vendor CLI on
/// PATH and authenticated». One adapter per vendor (claude → <c>claude auth status</c>,
/// codex → <c>codex login status</c>); the shared run/map logic lives in the Infrastructure
/// base class. Mirrors <see cref="Capabilities.ICapabilityProbe"/> but answers per *vendor*
/// rather than per capability — vendor login is surfaced on the terminal vendor catalog
/// (`GET /terminal/vendors`) and feeds the «Throne готов» readiness check, not the
/// capability toggle list.
///
/// Like the capability probes, an adapter must never throw on a missing binary or an
/// unauthenticated CLI — it folds both into a non-<see cref="AgentVendorLoginStatus.Ready"/>
/// result.
/// </summary>
public interface IAgentVendorLoginProbe
{
    /// <summary>Vendor token this probe answers for (<see cref="TerminalAgentCatalog.VendorClaude"/>, …).</summary>
    string Vendor { get; }

    Task<AgentVendorLoginResult> ProbeAsync(CancellationToken ct);
}

/// <summary>
/// Login readiness of a vendor CLI.
/// </summary>
public enum AgentVendorLoginStatus
{
    /// <summary>CLI on PATH and authenticated (status command exited 0).</summary>
    Ready,

    /// <summary>CLI present but not authenticated (status command exited non-zero).</summary>
    LoggedOut,

    /// <summary>CLI not found on PATH.</summary>
    Missing,
}

/// <summary>
/// Outcome of one vendor login probe pass.
/// </summary>
/// <param name="Status">Login readiness.</param>
/// <param name="Detail">Short free-form note (`claude 2.x`, `codex: not logged in`) — surfaced to the UI.</param>
public sealed record AgentVendorLoginResult(AgentVendorLoginStatus Status, string? Detail);
