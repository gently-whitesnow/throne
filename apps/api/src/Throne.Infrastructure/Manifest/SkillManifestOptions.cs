namespace Throne.Infrastructure.Manifest;

public sealed class SkillManifestOptions
{
    public const string SectionName = "Throne:SkillManifest";

    public string Path { get; set; } = "specs/manifest/throne-skills.yaml";
}
