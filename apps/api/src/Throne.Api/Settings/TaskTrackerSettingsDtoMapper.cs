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
        TaskTrackerConnectionHealth.Auth => TaskTrackerConnectionState.Auth,
        TaskTrackerConnectionHealth.Offline => TaskTrackerConnectionState.Offline,
        TaskTrackerConnectionHealth.Blocked => TaskTrackerConnectionState.Blocked,
        _ => TaskTrackerConnectionState.Offline,
    };

    public static TaskTrackerBoardSearchDto SearchResult(
        string tracker,
        IReadOnlyList<TaskTrackerBoardMatch> matches) => new()
        {
            Tracker = tracker,
            Boards = matches.Select(m => new TaskTrackerBoardMatchDto
            {
                Board_id = m.BoardId,
                Board_title = m.BoardTitle,
                Space_id = m.SpaceId,
                Space_title = m.SpaceTitle,
            }).ToList<TaskTrackerBoardMatchDto>(),
        };

    public static TaskTrackerBoardSelectionDto SelectionView(
        string tracker,
        IReadOnlyList<TaskTrackerBoardSelection> selection) => new()
        {
            Tracker = tracker,
            Boards = selection.Select(s => new TaskTrackerBoardSelectionEntry
            {
                Space_id = s.SpaceId,
                Space_title = s.SpaceTitle!,
                Board_id = s.BoardId,
                Board_title = s.BoardTitle!,
                Context_field = ToContextField(s.ContextField),
            }).ToList<TaskTrackerBoardSelectionEntry>(),
        };

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
