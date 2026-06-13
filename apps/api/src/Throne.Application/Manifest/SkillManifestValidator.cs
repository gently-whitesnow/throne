using Throne.Domain.PromptParts;

namespace Throne.Application.Manifest;

internal static class SkillManifestValidator
{
    public static void Validate(SkillManifest m)
    {
        if (m.Version != 1)
        {
            throw new SkillManifestException($"Unsupported manifest version {m.Version}; expected 1.");
        }

        var knownScopes = new HashSet<string>(PromptPartScopeNames.All, StringComparer.Ordinal);
        // 'kind' in the manifest is a prompt-part key — no closed whitelist (ADR-0036), only
        // non-emptiness and per-section uniqueness are enforced.
        var systemKeys = SystemInstructionsValidator.Validate(m.SystemInstructions);
        BundlesValidator.Validate(m.Bundles, knownScopes, systemKeys);
        DreamSourcesValidator.Validate(m.DreamSources);
    }
}

internal static class DreamSourcesValidator
{
    public static void Validate(IReadOnlyList<DreamSourceManifestEntry> sources)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in sources)
        {
            if (string.IsNullOrWhiteSpace(s.Vendor))
            {
                throw new SkillManifestException("dream_sources: empty vendor.");
            }
            if (string.IsNullOrWhiteSpace(s.Path))
            {
                throw new SkillManifestException($"dream_sources: '{s.Vendor}' has empty path.");
            }
            if (!seen.Add(s.Vendor))
            {
                throw new SkillManifestException($"dream_sources: duplicate vendor '{s.Vendor}'.");
            }
        }
    }
}

internal static class SystemInstructionsValidator
{
    public static HashSet<string> Validate(IReadOnlyList<SystemInstructionEntry> systemInstructions)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in systemInstructions)
        {
            ValidateOne(s, seen);
        }
        return seen;
    }

    private static void ValidateOne(SystemInstructionEntry s, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(s.Kind))
        {
            throw new SkillManifestException("system_instructions: empty key.");
        }
        if (!seen.Add(s.Kind))
        {
            throw new SkillManifestException($"system_instructions: duplicate key '{s.Kind}'.");
        }
        if (string.IsNullOrWhiteSpace(s.Text))
        {
            throw new SkillManifestException($"system_instructions: empty text for key '{s.Kind}'.");
        }
    }
}

internal static class BundlesValidator
{
    public static void Validate(
        IReadOnlyList<BundleDefinition> bundles,
        HashSet<string> knownScopes,
        HashSet<string> systemKeys)
    {
        var modes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in bundles)
        {
            ValidateBundle(b, knownScopes, systemKeys, modes);
        }
    }

    private static void ValidateBundle(
        BundleDefinition b,
        HashSet<string> knownScopes,
        HashSet<string> systemKeys,
        HashSet<string> modes)
    {
        if (string.IsNullOrWhiteSpace(b.Mode))
        {
            throw new SkillManifestException("bundles: empty mode.");
        }
        if (!modes.Add(b.Mode))
        {
            throw new SkillManifestException($"bundles: duplicate mode '{b.Mode}'.");
        }
        if (b.Includes.Count == 0)
        {
            throw new SkillManifestException($"bundles: '{b.Mode}' has no includes.");
        }
        foreach (var inc in b.Includes)
        {
            BundleIncludeValidator.Validate(b.Mode, inc, knownScopes, systemKeys);
        }
    }
}

internal static class BundleIncludeValidator
{
    public static void Validate(
        string mode,
        BundleInclude inc,
        HashSet<string> knownScopes,
        HashSet<string> systemKeys)
    {
        if (!knownScopes.Contains(inc.Scope))
        {
            throw new SkillManifestException($"bundles: '{mode}' has unknown scope '{inc.Scope}'.");
        }
        if (string.IsNullOrWhiteSpace(inc.Kind))
        {
            throw new SkillManifestException($"bundles: '{mode}' has an empty key.");
        }
        if (string.Equals(inc.Scope, PromptPartScopeNames.System, StringComparison.Ordinal)
            && !systemKeys.Contains(inc.Kind))
        {
            throw new SkillManifestException(
                $"bundles: '{mode}' references system key '{inc.Kind}' that has no text in system_instructions.");
        }
    }
}
