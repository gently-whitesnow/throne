using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Domain.Tests.TaskTrackers;

internal static class IntentCardAttachmentTestBuilder
{
    public static readonly DateTimeOffset Now = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    public static CardSnapshot Snapshot(string title = "Card title", DateTimeOffset? fetchedAt = null) =>
        new(
            Title: title,
            Description: "body",
            ColumnTitle: "In Progress",
            Archived: false,
            CardVersion: "v1",
            FetchedAt: fetchedAt ?? Now);

    public static CardCoordinate Coordinate(string tracker = "kaiten", string boardId = "10", string cardId = "42") =>
        new(tracker, boardId, cardId);

    public static IntentCardAttachment Attached(
        string intentId = "intent-abc",
        string tracker = "kaiten",
        string boardId = "10",
        string cardId = "42",
        DateTimeOffset? now = null) =>
        IntentCardAttachment.Create(
            id: CardAttachmentId.New(),
            intentId: new IntentId(intentId),
            coordinate: Coordinate(tracker, boardId, cardId),
            snapshot: Snapshot(fetchedAt: now ?? Now),
            now: now ?? Now);
}
