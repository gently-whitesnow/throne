using Throne.Domain.PromptParts;
using Throne.PromptParts.Contracts.Generated;

namespace Throne.Api.PromptParts;

internal static class PromptPartDtoMapper
{
    private const int TextShortMaxLength = 140;

    public static PromptPartDto ToDetailDto(PromptPart part) => new()
    {
        Id = part.Id.Value,
        Key = part.Key,
        Scope = part.Scope,
        Text = part.Text,
        Description = part.Description,
        Current_version = part.CurrentVersion,
        Mode_roles = ToRoleDtos(part.ModeRoles),
        Created_at = part.CreatedAt,
        Updated_at = part.UpdatedAt,
    };

    public static PromptPartListItemDto ToListDto(PromptPart part) => new()
    {
        Id = part.Id.Value,
        Key = part.Key,
        Scope = part.Scope,
        Description = part.Description,
        Text_short = TextShort(part.Text),
        Current_version = part.CurrentVersion,
        Mode_roles = ToRoleDtos(part.ModeRoles),
        Created_at = part.CreatedAt,
        Updated_at = part.UpdatedAt,
    };

    public static IReadOnlyList<PromptPartModeRole> ToDomainRoles(ICollection<PromptPartModeRoleDto>? roles) =>
        roles is null
            ? []
            : roles.Select(r => new PromptPartModeRole(r.Mode, r.Role, r.Order)).ToList();

    private static List<PromptPartModeRoleDto> ToRoleDtos(IReadOnlyList<PromptPartModeRole> roles) =>
        roles.Select(r => new PromptPartModeRoleDto { Mode = r.Mode, Role = r.Role, Order = r.Order }).ToList();

    private static string TextShort(string text) =>
        text.Length <= TextShortMaxLength ? text : text[..TextShortMaxLength];
}
