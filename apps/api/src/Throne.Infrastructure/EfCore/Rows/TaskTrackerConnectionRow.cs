namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>task_tracker_connections</c> table. One row per tracker key holds the
/// workspace base URL, the API token (stored as-is — Throne is local-first/single-operator, ADR-0029)
/// and the operator's board selection as a JSON list. A connection and its selection share a row so
/// deleting the connection drops the selection with it.
/// </summary>
internal sealed class TaskTrackerConnectionRow
{
    public string Tracker { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public List<TaskTrackerBoardSelectionRow> SelectedBoards { get; set; } = [];
}

/// <summary>One selected board with its grouping context, serialized inside the connection row.</summary>
internal sealed class TaskTrackerBoardSelectionRow
{
    public string SpaceId { get; set; } = string.Empty;
    public string? SpaceTitle { get; set; }
    public string BoardId { get; set; } = string.Empty;
    public string? BoardTitle { get; set; }
    public string ContextField { get; set; } = string.Empty;
}
