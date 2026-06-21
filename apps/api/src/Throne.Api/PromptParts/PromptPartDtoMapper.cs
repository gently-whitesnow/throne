using Throne.Application.PromptPartPatches;
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

    public static CurrentPromptPartDto ToCurrentDto(CurrentPromptPartView view) => new()
    {
        Prompt_part_id = view.PromptPartId,
        Scope = view.Scope,
        Key = view.Key,
        Text = view.Text,
        Current_version = view.CurrentVersion,
        Updated_at = view.UpdatedAt,
    };

    public static string FromTargetScope(Target_scope scope) => scope switch
    {
        Target_scope.System => PromptPartScopeNames.System,
        Target_scope.User => PromptPartScopeNames.User,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown prompt part scope."),
    };

    private static List<PromptPartModeRoleDto> ToRoleDtos(IReadOnlyList<PromptPartModeRole> roles) =>
        roles.Select(r => new PromptPartModeRoleDto { Mode = r.Mode, Role = r.Role, Order = r.Order }).ToList();

    private static string TextShort(string text) =>
        text.Length <= TextShortMaxLength ? text : text[..TextShortMaxLength];
}
