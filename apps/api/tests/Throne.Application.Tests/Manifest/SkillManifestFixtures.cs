using Throne.Application.Manifest;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;


namespace Throne.Application.Tests.Manifest;

internal static class SkillManifestFixtures
{
    public static readonly IReadOnlyList<string> Keys = ["common", "interview", "work", "dream", "schema_map"];

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
            Bundle(PromptBundleModeNames.Dream, "dream"),
            schemaMapBundle,
        };

        return new SkillManifest(1, systemInstructions, bundles, Array.Empty<DreamSourceManifestEntry>());
    }

    public static InMemorySkillManifestProvider Provider() => new(Sample());

    /// <summary>
    /// Manifest where the <c>work</c> bundle includes the standalone-only <c>finale_work</c>
    /// system part. The embedded resolver filters it out; the standalone MCP path keeps it.
    /// </summary>
    public static SkillManifest FinaleSample()
    {
        string[] keys = ["common", "work", "finale_work"];
        var systemInstructions = keys
            .Select(key => new SystemInstructionEntry(key, $"system text for {key}"))
            .ToArray();

        var work = new BundleDefinition(
            PromptBundleModeNames.Work,
            [
                new BundleInclude(PromptPartScopeNames.System, "common"),
                new BundleInclude(PromptPartScopeNames.System, "work"),
                new BundleInclude(PromptPartScopeNames.System, "finale_work"),
                new BundleInclude(PromptPartScopeNames.User, "common"),
                new BundleInclude(PromptPartScopeNames.User, "work"),
            ]);

        return new SkillManifest(1, systemInstructions, new[] { work }, Array.Empty<DreamSourceManifestEntry>());
    }

    public static InMemorySkillManifestProvider FinaleProvider() => new(FinaleSample());
}
