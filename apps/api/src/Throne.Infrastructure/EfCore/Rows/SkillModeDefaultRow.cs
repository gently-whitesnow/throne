namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>skill_mode_defaults</c> table: one row per
/// <c>(mode, skill_id)</c> pair. The composite UNIQUE matches the Mongo id format
/// (<c>"{mode}:{skill_id}"</c>) so the seeder-style upsert stays idempotent.
/// </summary>
internal sealed class SkillModeDefaultRow
{
    public string Id { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string SkillId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
