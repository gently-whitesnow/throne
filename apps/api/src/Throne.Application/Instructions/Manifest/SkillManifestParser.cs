using Throne.Domain.Instructions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Throne.Application.Instructions.Manifest;

public static class SkillManifestParser
{
    public static SkillManifest Parse(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var raw = deserializer.Deserialize<RawManifest>(yaml)
                  ?? throw new SkillManifestException("Manifest YAML is empty.");

        var manifest = new SkillManifest(
            Version: raw.Version,
            SystemInstructions: raw.SystemInstructions
                .Select(e => new SystemInstructionEntry(e.Kind ?? "", e.Text ?? ""))
                .ToArray(),
            Bundles: raw.Bundles
                .Select(b => new BundleDefinition(
                    Mode: b.Mode ?? "",
                    Includes: b.Includes
                        .Select(i => new BundleInclude(i.Scope ?? "", i.Kind ?? ""))
                        .ToArray()))
                .ToArray(),
            Skills: raw.Skills
                .Select(s => new SkillDefinition(
                    Name: s.Name ?? "",
                    Description: s.Description ?? "",
                    BundleMode: s.BundleMode ?? "",
                    LauncherBody: NormalizeBody(s.LauncherBody)))
                .ToArray());

        Validate(manifest);
        return manifest;
    }

    private static string NormalizeBody(string? body) =>
        body is null ? string.Empty : body.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static void Validate(SkillManifest m)
    {
        if (m.Version != 1)
        {
            throw new SkillManifestException($"Unsupported manifest version {m.Version}; expected 1.");
        }

        var knownKinds = new HashSet<string>(InstructionKindNames.All, StringComparer.Ordinal);
        var knownScopes = new HashSet<string>(InstructionScopeNames.All, StringComparer.Ordinal);

        var systemKinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in m.SystemInstructions)
        {
            if (!knownKinds.Contains(s.Kind))
            {
                throw new SkillManifestException($"system_instructions: unknown kind '{s.Kind}'.");
            }
            if (!systemKinds.Add(s.Kind))
            {
                throw new SkillManifestException($"system_instructions: duplicate kind '{s.Kind}'.");
            }
            if (string.IsNullOrWhiteSpace(s.Text))
            {
                throw new SkillManifestException($"system_instructions: empty text for kind '{s.Kind}'.");
            }
        }

        var bundleModes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in m.Bundles)
        {
            if (string.IsNullOrWhiteSpace(b.Mode))
            {
                throw new SkillManifestException("bundles: empty mode.");
            }
            if (!bundleModes.Add(b.Mode))
            {
                throw new SkillManifestException($"bundles: duplicate mode '{b.Mode}'.");
            }
            if (b.Includes.Count == 0)
            {
                throw new SkillManifestException($"bundles: '{b.Mode}' has no includes.");
            }

            foreach (var inc in b.Includes)
            {
                if (!knownScopes.Contains(inc.Scope))
                {
                    throw new SkillManifestException($"bundles: '{b.Mode}' has unknown scope '{inc.Scope}'.");
                }
                if (!knownKinds.Contains(inc.Kind))
                {
                    throw new SkillManifestException($"bundles: '{b.Mode}' has unknown kind '{inc.Kind}'.");
                }
                if (string.Equals(inc.Scope, InstructionScopeNames.System, StringComparison.Ordinal)
                    && !systemKinds.Contains(inc.Kind))
                {
                    throw new SkillManifestException(
                        $"bundles: '{b.Mode}' references system kind '{inc.Kind}' that has no text in system_instructions.");
                }
            }
        }

        var skillNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in m.Skills)
        {
            if (string.IsNullOrWhiteSpace(s.Name))
            {
                throw new SkillManifestException("skills: empty name.");
            }
            if (!skillNames.Add(s.Name))
            {
                throw new SkillManifestException($"skills: duplicate name '{s.Name}'.");
            }
            if (string.IsNullOrWhiteSpace(s.Description))
            {
                throw new SkillManifestException($"skills: '{s.Name}' has no description.");
            }
            if (!bundleModes.Contains(s.BundleMode))
            {
                throw new SkillManifestException(
                    $"skills: '{s.Name}' references bundle_mode '{s.BundleMode}' that is not in bundles.");
            }
            if (string.IsNullOrWhiteSpace(s.LauncherBody))
            {
                throw new SkillManifestException($"skills: '{s.Name}' has no launcher_body.");
            }
        }
    }

    private sealed class RawManifest
    {
        public int Version { get; set; }
        public List<RawSystemInstruction> SystemInstructions { get; set; } = new();
        public List<RawBundle> Bundles { get; set; } = new();
        public List<RawSkill> Skills { get; set; } = new();
    }

    private sealed class RawSystemInstruction
    {
        public string? Kind { get; set; }
        public string? Text { get; set; }
    }

    private sealed class RawBundle
    {
        public string? Mode { get; set; }
        public List<RawInclude> Includes { get; set; } = new();
    }

    private sealed class RawInclude
    {
        public string? Scope { get; set; }
        public string? Kind { get; set; }
    }

    private sealed class RawSkill
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? BundleMode { get; set; }
        public string? LauncherBody { get; set; }
    }
}

public sealed class SkillManifestException(string message) : Exception(message);
