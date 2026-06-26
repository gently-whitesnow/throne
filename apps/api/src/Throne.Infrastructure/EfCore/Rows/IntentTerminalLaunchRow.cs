namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>terminal_launches</c> table (ADR-0041). Keyed by intent id;
/// at most one row per intent. <c>Effort</c> is nullable (effort-less vendors leave it
/// unset). <c>AttachedSkillIds</c> and <c>SelectedSkillIdsByMode</c> are JSON columns —
/// nothing queries inside them and the per-mode shape is keyed by wire-string modes.
/// </summary>
internal sealed class IntentTerminalLaunchRow
{
    public string Id { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Effort { get; set; }
    public List<string>? AttachedSkillIds { get; set; }
    public Dictionary<string, List<string>>? SelectedSkillIdsByMode { get; set; }
}
