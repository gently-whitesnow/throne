namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Single source of truth for «is the prerequisite for capability <c>X</c> currently
/// available on the host». One probe per capability key (`tmux`, `code`, `gh`).
/// Detection NEVER flips the persisted <c>enabled</c> toggle — that is explicit opt-in
/// (ADR-0026 § 1).
/// </summary>
public interface ICapabilityProbe
{
    /// <summary>Capability key this probe answers for (`tmux` / `code` / `gh`).</summary>
    string CapabilityName { get; }

    /// <summary>
    /// Run the underlying health-check (`tmux --version`, `gh auth status`, ...). Must
    /// never throw on a missing binary or an unauthenticated CLI — fold both into
    /// <see cref="CapabilityProbeResult.Detected"/> = <c>false</c>.
    /// </summary>
    Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct);
}

/// <summary>
/// Outcome of one probe pass.
/// </summary>
/// <param name="Detected">Whether the prerequisite is currently satisfied.</param>
/// <param name="Detail">Short free-form note (`tmux 3.5a`, `gh: not authenticated`) — surfaced to the UI.</param>
public sealed record CapabilityProbeResult(bool Detected, string? Detail);
