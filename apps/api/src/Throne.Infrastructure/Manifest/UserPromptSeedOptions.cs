namespace Throne.Infrastructure.Manifest;

public sealed class UserPromptSeedOptions
{
    public const string SectionName = "Throne:UserPromptSeed";

    public string Path { get; set; } = "specs/manifest/throne-user-prompt-seed-parts.yaml";
}
