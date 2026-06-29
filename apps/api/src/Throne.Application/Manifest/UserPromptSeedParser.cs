using Throne.Domain.PromptParts;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Throne.Application.Manifest;

public static class UserPromptSeedParser
{
    public static UserPromptSeed Parse(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var raw = deserializer.Deserialize<RawSeed>(yaml)
                  ?? throw new SkillManifestException("User prompt seed YAML is empty.");

        var seed = new UserPromptSeed(
            Version: raw.Version,
            Parts: raw.SeedParts
                .Select(ToPart)
                .ToArray());

        Validate(seed);
        return seed;
    }

    private static UserPromptSeedPart ToPart(RawSeedPart p) =>
        new(
            Key: p.Key ?? "",
            Text: p.Text ?? "",
            Description: p.Description,
            ModeRoles: p.ModeRoles
                .Select(r => new PromptPartModeRole(r.Mode ?? "", r.Role ?? "", r.Order))
                .ToArray());

    private static void Validate(UserPromptSeed seed)
    {
        if (seed.Version != 1)
        {
            throw new SkillManifestException($"Unsupported user prompt seed version {seed.Version}; expected 1.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in seed.Parts)
        {
            if (string.IsNullOrWhiteSpace(part.Key))
            {
                throw new SkillManifestException("seed_parts: empty key.");
            }
            if (!seen.Add(part.Key))
            {
                throw new SkillManifestException($"seed_parts: duplicate key '{part.Key}'.");
            }
            if (string.IsNullOrWhiteSpace(part.Text))
            {
                throw new SkillManifestException($"seed_parts: empty text for key '{part.Key}'.");
            }
            try
            {
                // Reuse the domain invariants (known mode/role, non-negative order, one role
                // per mode) so the seed cannot describe a part the aggregate would reject.
                PromptPart.ValidateModeRoles(part.ModeRoles);
            }
            catch (ArgumentException ex)
            {
                throw new SkillManifestException($"seed_parts: '{part.Key}' has invalid mode_roles: {ex.Message}");
            }
        }
    }

    private sealed class RawSeed
    {
        public int Version { get; set; }
        public List<RawSeedPart> SeedParts { get; set; } = new();
    }

    private sealed class RawSeedPart
    {
        public string? Key { get; set; }
        public string? Text { get; set; }
        public string? Description { get; set; }
        public List<RawRole> ModeRoles { get; set; } = new();
    }

    private sealed class RawRole
    {
        public string? Mode { get; set; }
        public string? Role { get; set; }
        public int Order { get; set; }
    }
}
