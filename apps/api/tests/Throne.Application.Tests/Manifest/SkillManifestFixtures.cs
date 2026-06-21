using Throne.Application.Manifest;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;


namespace Throne.Application.Tests.Manifest;

internal static class SkillManifestFixtures
{
    public static readonly IReadOnlyList<string> Keys = ["interview", "work", "review"];

    public static SkillManifest Sample()
    {
        var systemInstructions = Keys
            .Select(key => new SystemInstructionEntry(key, $"system text for {key}"))
            .ToArray();

        BundleDefinition Bundle(string mode, string key) => new(
            Mode: mode,
            Includes:
            [
                new BundleInclude(PromptPartScopeNames.System, key),
                new BundleInclude(PromptPartScopeNames.User, "common"),
                new BundleInclude(PromptPartScopeNames.User, key),
            ]);

        var bundles = new[]
        {
            Bundle(PromptPartModeNames.Interview, "interview"),
            Bundle(PromptPartModeNames.Work, "work"),
            Bundle(PromptPartModeNames.Review, "review"),
        };

        return new SkillManifest(1, systemInstructions, bundles, Array.Empty<DreamSourceManifestEntry>());
    }

    public static InMemorySkillManifestProvider Provider() => new(Sample());
}
