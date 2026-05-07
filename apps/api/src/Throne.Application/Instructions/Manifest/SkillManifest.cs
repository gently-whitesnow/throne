namespace Throne.Application.Instructions.Manifest;

public sealed record SkillManifest(
    int Version,
    IReadOnlyList<SystemInstructionEntry> SystemInstructions,
    IReadOnlyList<BundleDefinition> Bundles);

public sealed record SystemInstructionEntry(string Kind, string Text);

public sealed record BundleDefinition(string Mode, IReadOnlyList<BundleInclude> Includes);

public sealed record BundleInclude(string Scope, string Kind);
