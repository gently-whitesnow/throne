namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>prompt_part_patches</c> table. Mirrors
/// <c>PromptPartPatchDocument</c> field-for-field; <c>mode_roles</c> is materialized as a
/// nullable JSON array so non-role operations leave the column NULL — symmetric with the
/// Mongo <c>BsonIgnoreIfNull</c> behavior.
/// </summary>
internal sealed class PromptPartPatchRow
{
    public string Id { get; set; } = string.Empty;
    public string TargetScope { get; set; } = string.Empty;
    public string TargetKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string PatchText { get; set; } = string.Empty;
    public List<PromptPartModeRolePayload>? ModeRoles { get; set; }
    public string? AppliedText { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public string? RejectComment { get; set; }
    public int BaseVersion { get; set; }
    public int? AppliedVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? IdempotencyKey { get; set; }
}
