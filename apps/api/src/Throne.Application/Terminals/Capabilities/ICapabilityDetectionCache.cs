namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// TTL-cached fan-out across all <see cref="ICapabilityProbe"/>s. Application/HTTP read
/// from this cache when materialising <c>CapabilityDto.detected</c> + <c>detection_detail</c>;
/// the cache itself decides when to call the underlying probe (per
/// <see cref="CapabilityDetectionOptions.DetectionTtlSeconds"/>). The persisted toggle
/// (<see cref="Throne.Domain.Capabilities.Capabilities"/>) is NEVER touched here.
/// </summary>
public interface ICapabilityDetectionCache
{
    /// <summary>
    /// Read the (possibly cached) probe result for <paramref name="capabilityName"/>.
    /// Returns <c>null</c> when no probe is registered for that key.
    /// </summary>
    Task<CapabilityProbeResult?> GetAsync(string capabilityName, CancellationToken ct);
}
