using Throne.Domain.PromptParts;

namespace Throne.Application.Manifest;

/// <summary>
/// First-run seed of editable user-scope prompt parts (ADR-0051). Distinct from
/// <see cref="SkillManifest"/>: these texts are generic starter placeholders that become
/// normal editable user parts once the seeder writes them, and only on a truly empty
/// <c>prompt_parts(scope=user)</c>.
/// </summary>
public sealed record UserPromptSeed(int Version, IReadOnlyList<UserPromptSeedPart> Parts);

public sealed record UserPromptSeedPart(
    string Key,
    string Text,
    string? Description,
    IReadOnlyList<PromptPartModeRole> ModeRoles);
