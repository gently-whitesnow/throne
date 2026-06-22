namespace Throne.Application.Manifest;

public sealed record SkillManifest(
    int Version,
    IReadOnlyList<SystemInstructionEntry> SystemInstructions,
    IReadOnlyList<BundleDefinition> Bundles,
    IReadOnlyList<DreamSourceManifestEntry> DreamSources);

public sealed record SystemInstructionEntry(string Kind, string Text);

public sealed record BundleDefinition(string Mode, IReadOnlyList<BundleInclude> Includes);

public sealed record BundleInclude(string Scope, string Kind);

/// <summary>
/// Manifest declaration of where the frontier agent should look for prior
/// conversations during a /dream pass. The server never reads these paths
/// itself — they are passed back to the agent through <c>skills/dream/bin/throne-dream sources</c>.
/// </summary>
public sealed record DreamSourceManifestEntry(string Vendor, string Path, string Hint);
