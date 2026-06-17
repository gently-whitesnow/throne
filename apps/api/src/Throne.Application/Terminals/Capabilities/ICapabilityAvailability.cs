namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Narrow port consumed by launch-time gates (vendor catalog filter, run pre-flight) that need
/// to know «is capability <c>X</c> currently usable» without pulling the full
/// <see cref="CapabilitiesService"/> dependency graph (persistence + detection cache) into
/// unit tests. Implemented by <see cref="CapabilitiesService"/>; the gate is true only when the
/// persisted toggle is on AND the live probe reports detected.
/// </summary>
public interface ICapabilityAvailability
{
    Task<bool> IsAvailableAsync(string name, CancellationToken ct);
}
