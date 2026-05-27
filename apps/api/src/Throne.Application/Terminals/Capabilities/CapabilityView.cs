namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Materialised view returned by <see cref="CapabilitiesService"/>. The HTTP layer maps
/// this directly to <c>CapabilityDto</c> — keeping the service layer free of NSwag
/// contract types and the controller free of detection/persistence plumbing.
/// </summary>
public sealed record CapabilityView(
    string Name,
    string Title,
    string Description,
    string PrerequisiteHint,
    bool Detected,
    string? DetectionDetail,
    bool Enabled);
