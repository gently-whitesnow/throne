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
            Contour: null,
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
            Contour: null,
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
    /// Manifest where <c>work</c> is split by execution contour (ADR-0034): the standalone bundle
    /// carries <c>finale_standalone</c>, the embedded one <c>finale_embedded</c>. Used to assert
    /// that each contour resolves its own finale instruction.
    /// </summary>
    public static SkillManifest ContourSample()
    {
        string[] keys = ["common", "work", "finale_standalone", "finale_embedded"];
        var systemInstructions = keys
            .Select(key => new SystemInstructionEntry(key, $"system text for {key}"))
            .ToArray();

        BundleDefinition WorkBundle(string contour, string finaleKey) => new(
            Mode: PromptBundleModeNames.Work,
            Contour: contour,
            Includes:
            [
                new BundleInclude(PromptPartScopeNames.System, "common"),
                new BundleInclude(PromptPartScopeNames.System, "work"),
                new BundleInclude(PromptPartScopeNames.System, finaleKey),
                new BundleInclude(PromptPartScopeNames.User, "common"),
                new BundleInclude(PromptPartScopeNames.User, "work"),
            ]);

        var bundles = new[]
        {
            WorkBundle(PromptContourNames.Standalone, "finale_standalone"),
            WorkBundle(PromptContourNames.Embedded, "finale_embedded"),
        };

        return new SkillManifest(1, systemInstructions, bundles, Array.Empty<DreamSourceManifestEntry>());
    }

    public static InMemorySkillManifestProvider ContourProvider() => new(ContourSample());
}
