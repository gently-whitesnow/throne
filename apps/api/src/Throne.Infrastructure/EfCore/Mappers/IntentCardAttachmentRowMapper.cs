using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentCardAttachmentRowMapper
{
    public static IntentCardAttachmentRow ToRow(IntentCardAttachment attachment)
    {
        var snapshot = attachment.State.Snapshot;
        return new IntentCardAttachmentRow
        {
            Id = attachment.Id.Value,
            IntentId = attachment.IntentId.Value,
            Tracker = attachment.Coordinate.Tracker,
            BoardId = attachment.Coordinate.BoardId,
            CardId = attachment.Coordinate.CardId,
            Title = snapshot.Title,
            Description = snapshot.Description,
            ColumnTitle = snapshot.ColumnTitle,
            Archived = snapshot.Archived,
            CardVersion = snapshot.CardVersion,
            Availability = attachment.State.Availability,
            FetchedAt = snapshot.FetchedAt,
            CreatedAt = attachment.CreatedAt,
            UpdatedAt = attachment.State.UpdatedAt,
        };
    }

    public static IntentCardAttachment ToDomain(IntentCardAttachmentRow row) =>
        IntentCardAttachment.Restore(new IntentCardAttachmentSnapshot(
            Id: new CardAttachmentId(row.Id),
            IntentId: new IntentId(row.IntentId),
            Coordinate: new CardCoordinate(row.Tracker, row.BoardId, row.CardId),
            Snapshot: new CardSnapshot(
                Title: row.Title,
                Description: row.Description,
                ColumnTitle: row.ColumnTitle,
                Archived: row.Archived,
                CardVersion: row.CardVersion,
                FetchedAt: row.FetchedAt),
            Availability: row.Availability,
            CreatedAt: row.CreatedAt,
            UpdatedAt: row.UpdatedAt));
}
