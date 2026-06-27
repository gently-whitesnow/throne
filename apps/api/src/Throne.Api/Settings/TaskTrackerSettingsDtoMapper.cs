using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings;

/// <summary>
/// Translates between the task-tracker settings wire DTOs and the provider-neutral application types.
/// The board grouping «context» is an enum on the wire but a plain token in persistence, so the
/// <c>none</c> fallback (no field maps cleanly) survives a round-trip unchanged.
/// </summary>
internal static class TaskTrackerSettingsDtoMapper
{
    public static TaskTrackerConnectionDto Connection(
        string tracker,
        string displayName,
        TaskTrackerConnectionState state,
        string? baseUrl,
        string? error) => new()
        {
            Tracker = tracker,
            Display_name = displayName,
            State = state,
            Base_url = baseUrl!,
            Error = error!,
        };

    public static TaskTrackerConnectionState ToState(TaskTrackerConnectionHealth health) => health switch
    {
        TaskTrackerConnectionHealth.Connected => TaskTrackerConnectionState.Connected,
        TaskTrackerConnectionHealth.Invalid => TaskTrackerConnectionState.Invalid,
        TaskTrackerConnectionHealth.Unreachable => TaskTrackerConnectionState.Unreachable,
        _ => TaskTrackerConnectionState.Unreachable,
    };

    public static TaskTrackerBoardsDto Boards(
        string tracker,
        IReadOnlyList<TaskTrackerSpaceTopology> topology,
        IReadOnlyList<TaskTrackerBoardSelection> selection)
    {
        var selectedByBoard = selection.GroupBy(s => s.BoardId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var spaces = topology.Select(space => new TaskTrackerSpaceDto
        {
            Space_id = space.SpaceId,
            Space_title = space.SpaceTitle,
            Boards = space.Boards.Select(board =>
            {
                var picked = selectedByBoard.GetValueOrDefault(board.BoardId);
                return new TaskTrackerBoardDto
                {
                    Board_id = board.BoardId,
                    Board_title = board.BoardTitle,
                    Selected = picked is not null,
                    Context_field = picked is null
                        ? TaskTrackerContextField.None
                        : ToContextField(picked.ContextField),
                };
            }).ToList<TaskTrackerBoardDto>(),
        }).ToList<TaskTrackerSpaceDto>();

        return new TaskTrackerBoardsDto { Tracker = tracker, Spaces = spaces };
    }

    public static List<TaskTrackerBoardSelection> Selection(UpdateTaskTrackerBoardsRequest body) =>
        body.Boards.Select(entry => new TaskTrackerBoardSelection(
            entry.Space_id,
            entry.Space_title,
            entry.Board_id,
            entry.Board_title,
            ToToken(entry.Context_field))).ToList();

    private static string ToToken(TaskTrackerContextField field) => field switch
    {
        TaskTrackerContextField.Lane => "lane",
        TaskTrackerContextField.Tags => "tags",
        TaskTrackerContextField.Type => "type",
        _ => "none",
    };

    private static TaskTrackerContextField ToContextField(string token) => token switch
    {
        "lane" => TaskTrackerContextField.Lane,
        "tags" => TaskTrackerContextField.Tags,
        "type" => TaskTrackerContextField.Type,
        _ => TaskTrackerContextField.None,
    };
}
