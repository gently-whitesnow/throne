namespace Throne.Application.Manifest;

public sealed record SkillManifest(
    int Version,
    IReadOnlyList<SystemInstructionEntry> SystemInstructions,
    IReadOnlyList<BundleDefinition> Bundles,
    IReadOnlyList<DreamSourceManifestEntry> DreamSources);

public sealed record SystemInstructionEntry(string Kind, string Text);

/// <summary>
/// A bundle composition for a mode. <paramref name="Contour"/> is the execution contour
/// (<c>standalone</c>/<c>embedded</c>, ADR-0034) the bundle targets, or <c>null</c> for a
/// contour-neutral bundle that resolves for any request.
/// </summary>
public sealed record BundleDefinition(string Mode, string? Contour, IReadOnlyList<BundleInclude> Includes);

public sealed record BundleInclude(string Scope, string Kind);

/// <summary>
/// Manifest declaration of where the frontier agent should look for prior
/// conversations during a /dream pass. The server never reads these paths
/// itself — they are passed back to the agent through
/// <c>mcp__throne__get_dream_sources</c>.
/// </summary>
public sealed record DreamSourceManifestEntry(string Vendor, string Path, string Hint);
