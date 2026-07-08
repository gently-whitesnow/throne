using Throne.Application.TaskTrackers;
using Throne.TaskTrackers.Contracts.Generated;

namespace Throne.Api.TaskTrackers;

/// <summary>
/// Projects the provider-neutral <see cref="TaskTrackerCard"/> onto the wire DTO for the card browser.
/// Read-only view — no attachment identity, no availability (the browser has neither attach nor refresh).
/// </summary>
internal static class TaskTrackerCardDtoMapper
{
    public static TaskTrackerCardDto ToDto(TaskTrackerCard card) => new()
    {
        Card_id = card.CardId,
        Board_id = card.BoardId,
        Column_id = card.ColumnId,
        Column_title = card.ColumnTitle,
        Title = card.Title,
        Description = card.Description,
        Updated_at = card.UpdatedAt,
        Archived = card.Archived,
        Card_version = card.RevisionTag,
    };

    public static TaskTrackerBoardCardsResponse ToResponse(IReadOnlyList<TaskTrackerCard> cards) => new()
    {
        Cards = cards.Select(ToDto).ToList(),
    };
}
