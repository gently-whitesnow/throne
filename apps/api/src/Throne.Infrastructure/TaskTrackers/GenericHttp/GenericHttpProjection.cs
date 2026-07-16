using Throne.Application.TaskTrackers;

namespace Throne.Infrastructure.TaskTrackers.GenericHttp;

internal static class GenericHttpProjection
{
    public static TaskTrackerSpaceTopology ToTopology(IReadOnlyList<GenericHttpBoardDto> boards) =>
        new(
            SpaceId: "custom-http",
            SpaceTitle: "Custom HTTP",
            Boards: boards
                .Select(b => new TaskTrackerBoardRef(b.BoardId, BoardTitle(b)))
                .ToList());

    public static TaskTrackerCard ToCard(GenericHttpCardDto card) =>
        new(
            CardId: card.CardId,
            BoardId: card.BoardId,
            ColumnId: card.ColumnId,
            ColumnTitle: card.ColumnTitle,
            Title: card.Title,
            Description: card.Description,
            UpdatedAt: card.UpdatedAt,
            ColumnChangedAt: null,
            Archived: card.Archived,
            RevisionTag: card.CardVersion,
            WebUrl: string.IsNullOrWhiteSpace(card.WebUrl) ? null : card.WebUrl);

    private static string BoardTitle(GenericHttpBoardDto board) =>
        board.BoardTitle ?? board.Title ?? board.BoardId;
}
