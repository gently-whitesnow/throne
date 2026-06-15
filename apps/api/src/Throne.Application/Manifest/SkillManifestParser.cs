using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Throne.Application.Manifest;

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
            DreamSources: raw.DreamSources
                .Select(d => new DreamSourceManifestEntry(d.Vendor ?? "", d.Path ?? "", d.Hint ?? ""))
                .ToArray());

        SkillManifestValidator.Validate(manifest);
        return manifest;
    }

    private sealed class RawManifest
    {
        public int Version { get; set; }
        public List<RawSystemInstruction> SystemInstructions { get; set; } = new();
        public List<RawBundle> Bundles { get; set; } = new();
        public List<RawDreamSource> DreamSources { get; set; } = new();
    }

    private sealed class RawDreamSource
    {
        public string? Vendor { get; set; }
        public string? Path { get; set; }
        public string? Hint { get; set; }
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
}

public sealed class SkillManifestException(string message) : Exception(message);
