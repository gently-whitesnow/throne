namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Provider-neutral projection of an external card, as the sync axis sees it. Carries only what the
/// mirror needs: identity, the opaque board position (<see cref="ColumnId"/>/<see cref="ColumnTitle"/>),
/// content, and the change cursors used to skip unchanged cards. No tracker-internal type crosses
/// this boundary (mirrors how <c>IGitProvider</c> hides the gh/glab CLIs).
/// </summary>
public sealed record TaskTrackerCard(
    string CardId,
    string BoardId,
    string? ColumnId,
    string? ColumnTitle,
    string Title,
    string? Description,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ColumnChangedAt,
    bool Archived);

/// <summary>
/// Write-through payload. Null fields are left untouched upstream (last-write-wins on the fields that
/// actually changed). <see cref="ColumnId"/> moves the card between columns.
/// </summary>
public sealed record TaskTrackerCardPatch(
    string? Title = null,
    string? Description = null,
    string? ColumnId = null);
