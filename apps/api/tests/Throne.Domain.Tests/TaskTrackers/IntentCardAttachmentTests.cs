using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;
using static Throne.Domain.Tests.TaskTrackers.IntentCardAttachmentTestBuilder;

namespace Throne.Domain.Tests.TaskTrackers;

public class IntentCardAttachmentTests
{
    [Fact(DisplayName = "Create стартует в available с переданным снапшотом")]
    public void Create_starts_available()
    {
        var attachment = Attached();

        attachment.State.Availability.Should().Be(CardAvailabilityNames.Available);
        attachment.State.Snapshot.Title.Should().Be("Card title");
        attachment.CreatedAt.Should().Be(Now);
        attachment.State.UpdatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "ApplySnapshot заменяет снапшот и возвращает availability в available")]
    public void ApplySnapshot_refreshes_and_marks_available()
    {
        var attachment = Attached();
        attachment.MarkUnavailable(CardAvailabilityNames.Gone, Now.AddMinutes(1));

        var later = Now.AddMinutes(2);
        attachment.ApplySnapshot(Snapshot(title: "Renamed", fetchedAt: later), later);

        attachment.State.Availability.Should().Be(CardAvailabilityNames.Available);
        attachment.State.Snapshot.Title.Should().Be("Renamed");
        attachment.State.Snapshot.FetchedAt.Should().Be(later);
        attachment.State.UpdatedAt.Should().Be(later);
    }

    [Theory(DisplayName = "MarkUnavailable(unavailable|gone) сохраняет снапшот, меняет только availability + updatedAt")]
    [InlineData(CardAvailabilityNames.Unavailable)]
    [InlineData(CardAvailabilityNames.Gone)]
    public void MarkUnavailable_keeps_snapshot(string availability)
    {
        var attachment = Attached();
        var priorSnapshot = attachment.State.Snapshot;

        var later = Now.AddMinutes(5);
        attachment.MarkUnavailable(availability, later);

        attachment.State.Availability.Should().Be(availability);
        attachment.State.Snapshot.Should().BeSameAs(priorSnapshot);
        attachment.State.UpdatedAt.Should().Be(later);
    }

    [Fact(DisplayName = "MarkUnavailable(available) отвергается — только unavailable|gone")]
    public void MarkUnavailable_rejects_available()
    {
        var attachment = Attached();

        var act = () => attachment.MarkUnavailable(CardAvailabilityNames.Available, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Restore ре-валидирует availability — неизвестное значение падает")]
    public void Restore_rejects_unknown_availability()
    {
        var act = () => IntentCardAttachment.Restore(new IntentCardAttachmentSnapshot(
            Id: CardAttachmentId.New(),
            IntentId: new IntentId("intent-abc"),
            Coordinate: Coordinate(),
            Snapshot: Snapshot(),
            Availability: "banana",
            CreatedAt: Now,
            UpdatedAt: Now));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Restore восстанавливает атачмент со всеми полями идентичности")]
    public void Restore_round_trips_identity()
    {
        var id = CardAttachmentId.New();
        var restored = IntentCardAttachment.Restore(new IntentCardAttachmentSnapshot(
            Id: id,
            IntentId: new IntentId("intent-xyz"),
            Coordinate: Coordinate(cardId: "99"),
            Snapshot: Snapshot(title: "Persisted"),
            Availability: CardAvailabilityNames.Gone,
            CreatedAt: Now,
            UpdatedAt: Now.AddMinutes(3)));

        restored.Id.Should().Be(id);
        restored.IntentId.Value.Should().Be("intent-xyz");
        restored.Coordinate.CardId.Should().Be("99");
        restored.State.Availability.Should().Be(CardAvailabilityNames.Gone);
        restored.State.Snapshot.Title.Should().Be("Persisted");
        restored.State.UpdatedAt.Should().Be(Now.AddMinutes(3));
    }

    [Theory(DisplayName = "CardCoordinate отвергает tracker с заглавными/пробелом/ведущим дефисом")]
    [InlineData("Kaiten")]
    [InlineData("kai ten")]
    [InlineData("-kaiten")]
    [InlineData("kaiten!")]
    public void Coordinate_rejects_malformed_tracker(string tracker)
    {
        var act = () => new CardCoordinate(tracker, "10", "42");

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "CardCoordinate отвергает пустой board_id / card_id")]
    [InlineData("", "42")]
    [InlineData("  ", "42")]
    [InlineData("10", "")]
    [InlineData("10", "   ")]
    public void Coordinate_rejects_empty_ids(string boardId, string cardId)
    {
        var act = () => new CardCoordinate("kaiten", boardId, cardId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "CardCoordinate принимает валидные wire-ключи трекера")]
    [InlineData("kaiten")]
    [InlineData("jira")]
    [InlineData("linear-2")]
    [InlineData("0tracker")]
    public void Coordinate_accepts_valid_tracker(string tracker)
    {
        var coordinate = new CardCoordinate(tracker, "board-1", "card-1");

        coordinate.Tracker.Should().Be(tracker);
        coordinate.BoardId.Should().Be("board-1");
        coordinate.CardId.Should().Be("card-1");
    }
}
