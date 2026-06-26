namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>terminal_launches</c> table (ADR-0041). Keyed by intent id;
/// at most one row per intent. <c>Effort</c> is nullable (effort-less vendors leave it
/// unset). <c>SelectedSkillIdsByMode</c> is a JSON column — nothing queries inside it and the
/// per-mode shape is keyed by wire-string modes.
/// </summary>
internal sealed class IntentTerminalLaunchRow
{
    public string Id { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Vendor { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Effort { get; set; }

    /// <summary>
    /// Orphaned legacy column: «what is loaded» now collapses into
    /// <see cref="SelectedSkillIdsByMode"/>. Kept mapped so no schema migration is needed —
    /// the app neither reads nor writes it; existing rows keep their value untouched.
    /// </summary>
    public List<string>? AttachedSkillIds { get; set; }

    public Dictionary<string, List<string>>? SelectedSkillIdsByMode { get; set; }
}
