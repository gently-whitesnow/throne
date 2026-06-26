using Throne.Domain.PromptParts;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class PromptPartRowMapper
{
    public static PromptPartRow ToRow(PromptPart part) => new()
    {
        Id = part.Id.Value,
        Scope = part.Scope,
        Key = part.Key,
        Text = part.Text,
        Description = part.Description,
        CurrentVersion = part.CurrentVersion,
        ModeRoles = part.ModeRoles.Count == 0
            ? []
            : [.. part.ModeRoles.Select(ToPayload)],
        CreatedAt = part.CreatedAt,
        UpdatedAt = part.UpdatedAt,
    };

    public static PromptPart ToDomain(PromptPartRow row) => PromptPart.Restore(
        id: new PromptPartId(row.Id),
        scope: row.Scope,
        key: row.Key,
        text: row.Text,
        description: row.Description,
        currentVersion: row.CurrentVersion,
        modeRoles: row.ModeRoles.Count == 0
            ? []
            : [.. row.ModeRoles.Select(ToDomainRole)],
        createdAt: row.CreatedAt,
        updatedAt: row.UpdatedAt);

    public static PromptPartModeRolePayload ToPayload(PromptPartModeRole role) => new()
    {
        Mode = role.Mode,
        Role = role.Role,
        Order = role.Order,
    };

    public static PromptPartModeRole ToDomainRole(PromptPartModeRolePayload payload) =>
        new(payload.Mode, payload.Role, payload.Order);
}
