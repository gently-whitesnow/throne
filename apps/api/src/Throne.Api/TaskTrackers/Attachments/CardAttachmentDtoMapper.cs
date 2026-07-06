using Throne.CardAttachments.Contracts.Generated;
using Throne.Domain.TaskTrackers;

namespace Throne.Api.TaskTrackers.Attachments;

/// <summary>
/// Wire-format translation for the card-attachment slice: domain <see cref="IntentCardAttachment"/> →
/// generated <see cref="CardAttachmentDto"/>, including the availability string → <see cref="CardAvailability"/>
/// enum projection (mirrors how <c>RepositoryEnumDtoMapper</c> maps <c>clone_status</c>).
/// </summary>
internal static class CardAttachmentDtoMapper
{
    public static CardAttachmentDto ToDto(IntentCardAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var snapshot = attachment.State.Snapshot;
        return new CardAttachmentDto
        {
            Id = attachment.Id.Value,
            Intent_id = attachment.IntentId.Value,
            Tracker = attachment.Coordinate.Tracker,
            Board_id = attachment.Coordinate.BoardId,
            Card_id = attachment.Coordinate.CardId,
            Title = snapshot.Title,
            Description = snapshot.Description,
            Column_title = snapshot.ColumnTitle,
            Archived = snapshot.Archived,
            Card_version = snapshot.CardVersion,
            Availability = ToWireAvailability(attachment.State.Availability),
            Fetched_at = snapshot.FetchedAt,
            Created_at = attachment.CreatedAt,
            Updated_at = attachment.State.UpdatedAt,
        };
    }

    public static CardAvailability ToWireAvailability(string value) => value switch
    {
        CardAvailabilityNames.Available => CardAvailability.Available,
        CardAvailabilityNames.Unavailable => CardAvailability.Unavailable,
        CardAvailabilityNames.Gone => CardAvailability.Gone,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown card availability."),
    };
}
