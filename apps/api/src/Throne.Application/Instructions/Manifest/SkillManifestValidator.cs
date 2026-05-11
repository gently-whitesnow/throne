using Throne.Domain.Instructions;

namespace Throne.Application.Instructions.Manifest;

internal static class SkillManifestValidator
{
    public static void Validate(SkillManifest m)
    {
        if (m.Version != 1)
        {
            throw new SkillManifestException($"Unsupported manifest version {m.Version}; expected 1.");
        }

        var knownKinds = new HashSet<string>(InstructionKindNames.All, StringComparer.Ordinal);
        var knownScopes = new HashSet<string>(InstructionScopeNames.All, StringComparer.Ordinal);
        var systemKinds = SystemInstructionsValidator.Validate(m.SystemInstructions, knownKinds);
        BundlesValidator.Validate(m.Bundles, knownKinds, knownScopes, systemKinds);
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
    public static HashSet<string> Validate(
        IReadOnlyList<SystemInstructionEntry> systemInstructions,
        HashSet<string> knownKinds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in systemInstructions)
        {
            ValidateOne(s, knownKinds, seen);
        }
        return seen;
    }

    private static void ValidateOne(SystemInstructionEntry s, HashSet<string> knownKinds, HashSet<string> seen)
    {
        if (!knownKinds.Contains(s.Kind))
        {
            throw new SkillManifestException($"system_instructions: unknown kind '{s.Kind}'.");
        }
        if (!seen.Add(s.Kind))
        {
            throw new SkillManifestException($"system_instructions: duplicate kind '{s.Kind}'.");
        }
        if (string.IsNullOrWhiteSpace(s.Text))
        {
            throw new SkillManifestException($"system_instructions: empty text for kind '{s.Kind}'.");
        }
    }
}

internal static class BundlesValidator
{
    public static void Validate(
        IReadOnlyList<BundleDefinition> bundles,
        HashSet<string> knownKinds,
        HashSet<string> knownScopes,
        HashSet<string> systemKinds)
    {
        var modes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in bundles)
        {
            ValidateBundle(b, knownKinds, knownScopes, systemKinds, modes);
        }
    }

    private static void ValidateBundle(
        BundleDefinition b,
        HashSet<string> knownKinds,
        HashSet<string> knownScopes,
        HashSet<string> systemKinds,
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
            BundleIncludeValidator.Validate(b.Mode, inc, knownKinds, knownScopes, systemKinds);
        }
    }
}

internal static class BundleIncludeValidator
{
    public static void Validate(
        string mode,
        BundleInclude inc,
        HashSet<string> knownKinds,
        HashSet<string> knownScopes,
        HashSet<string> systemKinds)
    {
        if (!knownScopes.Contains(inc.Scope))
        {
            throw new SkillManifestException($"bundles: '{mode}' has unknown scope '{inc.Scope}'.");
        }
        if (!knownKinds.Contains(inc.Kind))
        {
            throw new SkillManifestException($"bundles: '{mode}' has unknown kind '{inc.Kind}'.");
        }
        if (string.Equals(inc.Scope, InstructionScopeNames.System, StringComparison.Ordinal)
            && !systemKinds.Contains(inc.Kind))
        {
            throw new SkillManifestException(
                $"bundles: '{mode}' references system kind '{inc.Kind}' that has no text in system_instructions.");
        }
    }
}
