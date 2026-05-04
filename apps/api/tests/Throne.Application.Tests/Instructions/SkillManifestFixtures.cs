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

        var bundles = new[]
        {
            Bundle(InstructionBundleModeNames.Interview, InstructionKindNames.Interview),
            Bundle(InstructionBundleModeNames.Work, InstructionKindNames.Work),
            Bundle(InstructionBundleModeNames.Dream, InstructionKindNames.Dream),
            Bundle(InstructionBundleModeNames.Fix, InstructionKindNames.Fix),
        };

        var skills = new[]
        {
            new SkillDefinition("tinterview", "interview launcher", InstructionBundleModeNames.Interview, "interview body"),
            new SkillDefinition("twork", "work launcher", InstructionBundleModeNames.Work, "work body"),
            new SkillDefinition("tfix", "fix launcher", InstructionBundleModeNames.Fix, "fix body"),
            new SkillDefinition("tdream", "dream launcher", InstructionBundleModeNames.Dream, "dream body"),
        };

        return new SkillManifest(1, systemInstructions, bundles, skills);
    }

    public static InMemorySkillManifestProvider Provider() => new(Sample());
}
