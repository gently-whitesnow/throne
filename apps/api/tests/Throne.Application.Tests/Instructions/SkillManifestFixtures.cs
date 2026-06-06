using Throne.Application.Instructions;
using Throne.Application.Instructions.Manifest;
using Throne.Domain.Instructions;

namespace Throne.Application.Tests.Instructions;

internal static class SkillManifestFixtures
{
    public static SkillManifest Sample()
    {
        var systemInstructions = InstructionKindNames.All
            .Select(kind => new SystemInstructionEntry(kind, $"system text for {kind}"))
            .ToArray();

        BundleDefinition Bundle(string mode, string kind) => new(
            Mode: mode,
            Includes:
            [
                new BundleInclude(InstructionScopeNames.System, InstructionKindNames.Common),
                new BundleInclude(InstructionScopeNames.System, kind),
                new BundleInclude(InstructionScopeNames.User, InstructionKindNames.Common),
                new BundleInclude(InstructionScopeNames.User, kind),
            ]);

        // schema_map is launched without an intent and has no user-scope counterpart:
        // system common + system schema_map + user common only (mirrors the real manifest).
        var schemaMapBundle = new BundleDefinition(
            InstructionBundleModeNames.SchemaMap,
            [
                new BundleInclude(InstructionScopeNames.System, InstructionKindNames.Common),
                new BundleInclude(InstructionScopeNames.System, InstructionKindNames.SchemaMap),
                new BundleInclude(InstructionScopeNames.User, InstructionKindNames.Common),
            ]);

        var bundles = new[]
        {
            Bundle(InstructionBundleModeNames.Interview, InstructionKindNames.Interview),
            Bundle(InstructionBundleModeNames.Work, InstructionKindNames.Work),
            Bundle(InstructionBundleModeNames.Dream, InstructionKindNames.Dream),
            schemaMapBundle,
        };

        return new SkillManifest(1, systemInstructions, bundles, Array.Empty<DreamSourceManifestEntry>());
    }

    public static InMemorySkillManifestProvider Provider() => new(Sample());
}
