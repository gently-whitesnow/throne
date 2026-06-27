using FluentAssertions;
using Throne.Domain.TaskTrackers;

namespace Throne.Domain.Tests.TaskTrackers;

public class TaskTrackerCardLinkTests
{
    [Fact(DisplayName = "Указатель хранит стабильные идентификаторы tracker/board/card")]
    public void Stores_stable_identifiers()
    {
        var link = new TaskTrackerCardLink("kaiten", "board-7", "card-42");

        link.Tracker.Should().Be("kaiten");
        link.BoardId.Should().Be("board-7");
        link.CardId.Should().Be("card-42");
    }

    [Theory(DisplayName = "Пустые идентификаторы отклоняются")]
    [InlineData("", "b", "c")]
    [InlineData("kaiten", " ", "c")]
    [InlineData("kaiten", "b", "")]
    public void Rejects_blank_identifiers(string tracker, string board, string card)
    {
        var act = () => new TaskTrackerCardLink(tracker, board, card);

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Tracker key вне формы wire-ключа отклоняется")]
    [InlineData("Kaiten")]
    [InlineData("-kaiten")]
    [InlineData("kai_ten")]
    public void Rejects_malformed_tracker_key(string tracker)
    {
        var act = () => new TaskTrackerCardLink(tracker, "board", "card");

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(tracker));
    }
}
