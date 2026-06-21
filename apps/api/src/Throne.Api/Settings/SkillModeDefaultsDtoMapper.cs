using Throne.Application.Terminals;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings;

internal static class SkillModeDefaultsDtoMapper
{
    public static SkillModeDefaultsDto ToDto(SkillModeDefaultsView view) => new()
    {
        Skills = view.Skills.Select(skill => new SkillCatalogItemDto
        {
            Skill_id = skill.Id,
            Source = skill.Source,
            Title = skill.Title,
            Description = skill.Description,
        }).ToArray(),
        Modes = view.Modes.Select(mode => new SkillModeDefaultsModeDto
        {
            Mode = mode.Mode,
            Defaults = mode.Defaults.Select(item => new SkillModeDefaultDto
            {
                Skill_id = item.SkillId,
                Enabled = item.Enabled,
            }).ToArray(),
        }).ToArray(),
    };

    public static IReadOnlyList<SkillModeDefault> ToDomain(UpdateSkillModeDefaultsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Defaults.Select(item =>
            new SkillModeDefault(item.Mode, item.Skill_id, item.Enabled)).ToArray();
    }
}
