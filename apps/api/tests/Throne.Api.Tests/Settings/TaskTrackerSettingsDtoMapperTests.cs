using FluentAssertions;
using Throne.Api.Settings;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Tests.Settings;

public class TaskTrackerSettingsDtoMapperTests
{
    [Fact(DisplayName = "Boards: selected board carries its context; unselected falls back to none")]
    public void Boards_merges_selection()
    {
        var topology = new List<TaskTrackerSpaceTopology>
        {
            new("1", "Space", [new TaskTrackerBoardRef("10", "Picked"), new TaskTrackerBoardRef("11", "Other")]),
        };
        var selection = new List<TaskTrackerBoardSelection>
        {
            new("1", "Space", "10", "Picked", "tags"),
        };

        var dto = TaskTrackerSettingsDtoMapper.Boards("kaiten", topology, selection);

        var boards = dto.Spaces.Single().Boards.ToList();
        var picked = boards.Single(b => b.Board_id == "10");
        picked.Selected.Should().BeTrue();
        picked.Context_field.Should().Be(TaskTrackerContextField.Tags);

        var other = boards.Single(b => b.Board_id == "11");
        other.Selected.Should().BeFalse();
        other.Context_field.Should().Be(TaskTrackerContextField.None);
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
        TaskTrackerSettingsDtoMapper.ToState(TaskTrackerConnectionHealth.Invalid)
            .Should().Be(TaskTrackerConnectionState.Invalid);
        TaskTrackerSettingsDtoMapper.ToState(TaskTrackerConnectionHealth.Unreachable)
            .Should().Be(TaskTrackerConnectionState.Unreachable);
    }
}
