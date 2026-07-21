using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Infrastructure.Tests.EfCore.Persistence.IntentCardAttachments;

/// <summary>
/// Per-test scope: fresh migrated SQLite database + store instance. Integration-tagged — the
/// <c>intent_card_attachments</c> table lands with the ADR-0052 migration, so these run once that
/// migration is applied.
/// </summary>
internal sealed record IntentCardAttachmentTestScope(
    SqliteTestDatabase Database,
    IIntentCardAttachmentStore Store,
    IUnitOfWork UnitOfWork)
{
    public static async Task<IntentCardAttachmentTestScope> CreateAsync(SqliteFixture fixture)
    {
        var db = await fixture.CreateDatabaseAsync();
        return new IntentCardAttachmentTestScope(
            db,
            db.GetRequiredService<IIntentCardAttachmentStore>(),
            db.GetRequiredService<IUnitOfWork>());
    }
}

internal static class IntentCardAttachmentTestFactory
{
    public static readonly DateTimeOffset Now = new(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

    public static IntentCardAttachment NewAttachment(
        IntentId intentId,
        string tracker = "kaiten",
        string boardId = "10",
        string cardId = "42",
        string text = "Card text",
        DateTimeOffset? at = null) =>
        IntentCardAttachment.Create(
            id: CardAttachmentId.New(),
            intentId: intentId,
            coordinate: new CardCoordinate(tracker, boardId, cardId),
            snapshot: new CardSnapshot(
                Text: text,
                ColumnTitle: "In Progress",
                Archived: false,
                CardVersion: "v1",
                FetchedAt: at ?? Now),
            now: at ?? Now);
}
