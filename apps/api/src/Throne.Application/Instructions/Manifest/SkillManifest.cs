namespace Throne.Application.Instructions.Manifest;

public sealed record SkillManifest(
    int Version,
    IReadOnlyList<SystemInstructionEntry> SystemInstructions,
    IReadOnlyList<BundleDefinition> Bundles,
    IReadOnlyList<SkillDefinition> Skills);

public sealed record SystemInstructionEntry(string Kind, string Text);

public sealed record BundleDefinition(string Mode, IReadOnlyList<BundleInclude> Includes);

public sealed record BundleInclude(string Scope, string Kind);

public sealed record SkillDefinition(
    string Name,
    string Description,
    string BundleMode,
    string LauncherBody,
    bool Internal = false);
