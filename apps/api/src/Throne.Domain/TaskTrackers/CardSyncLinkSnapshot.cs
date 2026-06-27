namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Last content we know the card and the mirror agreed on. Echo suppression compares the live intent
/// text against <see cref="Title"/>/<see cref="Description"/> so a value just written by one side is
/// never bounced back to the other. <see cref="ColumnId"/> is the opaque board position (Throne does
/// not map it onto its own status enum); <see cref="ColumnTitle"/> is carried for display only.
/// </summary>
public sealed record CardSyncLinkSnapshot(
    string Title,
    string? Description,
    string? ColumnId,
    string? ColumnTitle);

/// <summary>
/// Upstream change cursors plus our last poll timestamp. <see cref="CardUpdatedAt"/> mirrors the
/// card's <c>updated</c> and <see cref="ColumnChangedAt"/> its <c>column_changed_at</c>; a poll skips
/// a card whose cursors did not advance.
/// </summary>
public sealed record CardSyncLinkCursors(
    DateTimeOffset? CardUpdatedAt,
    DateTimeOffset? ColumnChangedAt,
    DateTimeOffset? LastSyncedAt)
{
    public static readonly CardSyncLinkCursors Empty = new(null, null, null);
}
