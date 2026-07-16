namespace Throne.Application.TaskTrackers;

/// <summary>
/// Provider-neutral projection of an external card, as the task-tracker axis sees it. Carries only what a
/// consumer needs: identity, the opaque board position (<see cref="ColumnId"/>/<see cref="ColumnTitle"/>),
/// content, and the change cursors used to skip unchanged cards. No tracker-internal type crosses this
/// boundary (mirrors how <c>IGitProvider</c> hides the gh/glab CLIs).
/// <para>
/// <see cref="RevisionTag"/> is the opaque provider-supplied revision identifier (for Kaiten —
/// <c>card.version</c>); list/detail-asymmetric trackers fill it on both the list-row and the
/// detail response so a consumer can escalate from list to detail only when the tag actually
/// changed. Null means «provider did not supply a revision cursor».
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
    string? RevisionTag = null,
    string? WebUrl = null);
