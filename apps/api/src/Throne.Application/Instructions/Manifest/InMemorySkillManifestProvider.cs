namespace Throne.Application.Instructions.Manifest;

public sealed class InMemorySkillManifestProvider(SkillManifest manifest) : ISkillManifestProvider
{
    public SkillManifest Current { get; } = manifest ?? throw new ArgumentNullException(nameof(manifest));
}
