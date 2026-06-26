namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>prompt_parts</c> table. Mirrors <c>PromptPartDocument</c>
/// field-for-field; <c>mode_roles</c> is materialized as a JSON array column so the row
/// stays a single SQLite tuple without a join table — list shape is short, replace-only,
/// and never queried by individual role entry.
/// </summary>
internal sealed class PromptPartRow
{
    public string Id { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CurrentVersion { get; set; }
    public List<PromptPartModeRolePayload> ModeRoles { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// JSON shape for one entry inside <c>prompt_parts.mode_roles</c>. Field names match the
/// Mongo subdocument 1:1 so a SQLite dump reads identically against the OpenAPI contract.
/// </summary>
internal sealed class PromptPartModeRolePayload
{
    public string Mode { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Order { get; set; }
}
