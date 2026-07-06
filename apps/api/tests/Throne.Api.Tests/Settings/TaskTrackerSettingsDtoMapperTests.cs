using FluentAssertions;
using Throne.Api.Settings;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Tests.Settings;

public class TaskTrackerSettingsDtoMapperTests
{
    [Fact(DisplayName = "SearchResult: flattened matches carry board and space context")]
    public void SearchResult_maps_matches()
    {
        var matches = new List<TaskTrackerBoardMatch>
        {
            new("10", "Backlog", "1", "Engineering"),
        };

        var dto = TaskTrackerSettingsDtoMapper.SearchResult("kaiten", matches);

        dto.Tracker.Should().Be("kaiten");
        var board = dto.Boards.Single();
        board.Board_id.Should().Be("10");
        board.Board_title.Should().Be("Backlog");
        board.Space_id.Should().Be("1");
        board.Space_title.Should().Be("Engineering");
    }

    [Fact(DisplayName = "SelectionView: stored selection round-trips titles and context token to the wire enum")]
    public void SelectionView_maps_selection()
    {
        var selection = new List<TaskTrackerBoardSelection>
        {
            new("1", "Engineering", "10", "Backlog", "tags"),
        };

        var dto = TaskTrackerSettingsDtoMapper.SelectionView("kaiten", selection);

        dto.Tracker.Should().Be("kaiten");
        var entry = dto.Boards.Single();
        entry.Space_id.Should().Be("1");
        entry.Space_title.Should().Be("Engineering");
        entry.Board_id.Should().Be("10");
        entry.Board_title.Should().Be("Backlog");
        entry.Context_field.Should().Be(TaskTrackerContextField.Tags);
    }

    [Fact(DisplayName = "Selection: request entries round-trip the context-field token, defaulting to none")]
    public void Selection_maps_context_token()
    {
        var request = new UpdateTaskTrackerBoardsRequest
        {
            Boards =
            [
                new TaskTrackerBoardSelectionEntry
                {
                    Space_id = "1", Space_title = "S", Board_id = "10", Board_title = "B",
                    Context_field = TaskTrackerContextField.Lane,
                },
                new TaskTrackerBoardSelectionEntry
                {
                    Space_id = "1", Board_id = "11", Context_field = TaskTrackerContextField.None,
                },
            ],
        };

        var selection = TaskTrackerSettingsDtoMapper.Selection(request);

        selection.Should().HaveCount(2);
        selection[0].ContextField.Should().Be("lane");
        selection[1].ContextField.Should().Be("none");
        selection[1].BoardId.Should().Be("11");
    }

    [Fact(DisplayName = "ToState maps each probe health to its wire state")]
    public void ToState_maps_health()
    {
        TaskTrackerSettingsDtoMapper.ToState(TaskTrackerConnectionHealth.Connected)
            .Should().Be(TaskTrackerConnectionState.Connected);
        TaskTrackerSettingsDtoMapper.ToState(TaskTrackerConnectionHealth.Auth)
            .Should().Be(TaskTrackerConnectionState.Auth);
        TaskTrackerSettingsDtoMapper.ToState(TaskTrackerConnectionHealth.Offline)
            .Should().Be(TaskTrackerConnectionState.Offline);
        TaskTrackerSettingsDtoMapper.ToState(TaskTrackerConnectionHealth.Blocked)
            .Should().Be(TaskTrackerConnectionState.Blocked);
    }
}
