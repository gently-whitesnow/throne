namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>Persistence POCO: task_tracker_card_links table. One row per mirrored intent.</summary>
internal sealed class TaskTrackerCardLinkRow
{
    public string IntentId { get; set; } = string.Empty;
    public string Tracker { get; set; } = string.Empty;
    public string BoardId { get; set; } = string.Empty;
    public string CardId { get; set; } = string.Empty;
    public string? ColumnId { get; set; }
    public string? ColumnTitle { get; set; }
    public string SnapshotTitle { get; set; } = string.Empty;
    public string? SnapshotDescription { get; set; }
    public DateTimeOffset? CardUpdatedAt { get; set; }
    public DateTimeOffset? ColumnChangedAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? RevisionTag { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
