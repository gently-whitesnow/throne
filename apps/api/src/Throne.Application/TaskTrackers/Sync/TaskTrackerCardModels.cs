namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Provider-neutral projection of an external card, as the sync axis sees it. Carries only what the
/// mirror needs: identity, the opaque board position (<see cref="ColumnId"/>/<see cref="ColumnTitle"/>),
/// content, and the change cursors used to skip unchanged cards. No tracker-internal type crosses
/// this boundary (mirrors how <c>IGitProvider</c> hides the gh/glab CLIs).
/// <para>
/// <see cref="RevisionTag"/> is the opaque provider-supplied revision identifier (for Kaiten —
/// <c>card.version</c>); list/detail-asymmetric trackers fill it on both the list-row and the
/// detail response, so the sync workflow can escalate from list to detail only when the tag
/// actually changed. Null means «provider did not supply a revision cursor» → каждая итерация
/// форсирует detail-fetch.
/// </para>
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
    bool Archived,
    string? RevisionTag = null);

/// <summary>
/// Write-through payload. Null fields are left untouched upstream (last-write-wins on the fields that
/// actually changed). <see cref="ColumnId"/> moves the card between columns.
/// </summary>
public sealed record TaskTrackerCardPatch(
    string? Title = null,
    string? Description = null,
    string? ColumnId = null);
