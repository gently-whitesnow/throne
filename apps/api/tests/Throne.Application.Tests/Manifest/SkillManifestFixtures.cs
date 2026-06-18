using Throne.Application.Manifest;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;


namespace Throne.Application.Tests.Manifest;

internal static class SkillManifestFixtures
{
    public static readonly IReadOnlyList<string> Keys = ["common", "interview", "work", "review", "dream", "schema_map"];

    public static SkillManifest Sample()
    {
        var systemInstructions = Keys
            .Select(key => new SystemInstructionEntry(key, $"system text for {key}"))
            .ToArray();

        BundleDefinition Bundle(string mode, string key) => new(
            Mode: mode,
            Includes:
            [
                new BundleInclude(PromptPartScopeNames.System, "common"),
                new BundleInclude(PromptPartScopeNames.System, key),
                new BundleInclude(PromptPartScopeNames.User, "common"),
                new BundleInclude(PromptPartScopeNames.User, key),
            ]);

        // schema_map is launched without an intent and has no user-scope counterpart:
        // system common + system schema_map + user common only (mirrors the real manifest).
        var schemaMapBundle = new BundleDefinition(
            PromptBundleModeNames.SchemaMap,
            [
                new BundleInclude(PromptPartScopeNames.System, "common"),
                new BundleInclude(PromptPartScopeNames.System, "schema_map"),
                new BundleInclude(PromptPartScopeNames.User, "common"),
            ]);

        var bundles = new[]
        {
            Bundle(PromptBundleModeNames.Interview, "interview"),
            Bundle(PromptBundleModeNames.Work, "work"),
            Bundle(PromptBundleModeNames.Review, "review"),
            Bundle(PromptBundleModeNames.Dream, "dream"),
            schemaMapBundle,
        };

        return new SkillManifest(1, systemInstructions, bundles, Array.Empty<DreamSourceManifestEntry>());
    }

    public static InMemorySkillManifestProvider Provider() => new(Sample());
}
