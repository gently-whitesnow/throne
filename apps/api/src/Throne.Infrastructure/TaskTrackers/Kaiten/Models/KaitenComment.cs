namespace Throne.Infrastructure.TaskTrackers.Kaiten.Models;

/// <summary>A comment on a card.</summary>
internal sealed record KaitenComment(
    long Id,
    string Text,
    int Type,
    long CardId,
    long AuthorId,
    DateTimeOffset? Created,
    DateTimeOffset? Updated);

/// <summary>Payload for <c>POST /cards/{id}/comments</c>.</summary>
internal sealed record KaitenCreateCommentRequest(string Text, int? Type = null);
