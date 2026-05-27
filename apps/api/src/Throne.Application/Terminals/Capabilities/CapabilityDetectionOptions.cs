namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Configuration for <see cref="ICapabilityDetectionCache"/>. Slice 2 default keeps the
/// surface tiny: a single TTL covers every probe — there is no per-capability override
/// in MVP.
/// </summary>
public sealed class CapabilityDetectionOptions
{
    public const string SectionName = "Throne:Capabilities";

    /// <summary>
    /// How long a successful or failed probe result is reused before the next call to
    /// <see cref="ICapabilityDetectionCache.GetAsync"/> re-runs the probe. Default 60s
    /// (ADR-0026 § 1 + parent intent text).
    /// </summary>
    public int DetectionTtlSeconds { get; set; } = 60;
}
