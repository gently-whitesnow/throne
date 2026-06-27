using FluentAssertions;
using Throne.Domain.TaskTrackers;

namespace Throne.Domain.Tests.TaskTrackers;

public class CardSyncLinkTests
{
    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddHours(1);
    private static readonly DateTimeOffset T2 = T0.AddHours(2);

    private static readonly TaskTrackerCardLink Card = new("kaiten", "board-7", "card-42");
    private static readonly CardSyncLinkSnapshot Snap0 = new("Title", "Body", "col-1", "Todo");
    private static readonly CardSyncLinkCursors Cursors0 = new(T0, T0, T0);

    private static CardSyncLink NewLink() => CardSyncLink.Create("intent-1", Card, Snap0, Cursors0, T0);

    [Fact(DisplayName = "Create стартует в состоянии linked со снапшотом и курсорами")]
    public void Create_starts_linked()
    {
        var link = NewLink();

        link.IntentId.Should().Be("intent-1");
        link.Card.Should().BeSameAs(Card);
        link.Snapshot.Should().Be(Snap0);
        link.Cursors.Should().Be(Cursors0);
        link.State.Should().Be(CardSyncLinkState.Linked);
        link.IsStub.Should().BeFalse();
        link.CreatedAt.Should().Be(T0);
        link.UpdatedAt.Should().Be(T0);
    }

    [Fact(DisplayName = "Restore восстанавливает произвольное известное состояние и временные метки")]
    public void Restore_rehydrates_state()
    {
        var link = CardSyncLink.Restore("intent-1", Card, Snap0, Cursors0, CardSyncLinkState.Stub, T0, T1);

        link.State.Should().Be(CardSyncLinkState.Stub);
        link.IsStub.Should().BeTrue();
        link.CreatedAt.Should().Be(T0);
        link.UpdatedAt.Should().Be(T1);
    }

    [Fact(DisplayName = "Restore отклоняет неизвестное состояние")]
    public void Restore_rejects_unknown_state()
    {
        var act = () => CardSyncLink.Restore("intent-1", Card, Snap0, Cursors0, "weird", T0, T1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "RecordSnapshot обновляет снапшот, курсоры, метку и переводит в linked")]
    public void RecordSnapshot_updates_all()
    {
        var link = CardSyncLink.Restore("intent-1", Card, Snap0, Cursors0, CardSyncLinkState.Stub, T0, T0);
        var snap1 = new CardSyncLinkSnapshot("New", "NewBody", "col-2", "Doing");
        var cursors1 = new CardSyncLinkCursors(T1, T1, T1);

        link.RecordSnapshot(snap1, cursors1, T2);

        link.Snapshot.Should().Be(snap1);
        link.Cursors.Should().Be(cursors1);
        link.State.Should().Be(CardSyncLinkState.Linked);
        link.UpdatedAt.Should().Be(T2);
    }

    [Fact(DisplayName = "RecordPushedSnapshot меняет только снапшот, курсоры остаются прежними")]
    public void RecordPushedSnapshot_keeps_cursors()
    {
        var link = NewLink();
        var snap1 = new CardSyncLinkSnapshot("Pushed", "PushedBody", "col-1", "Todo");

        link.RecordPushedSnapshot(snap1, T1);

        link.Snapshot.Should().Be(snap1);
        link.Cursors.Should().Be(Cursors0);
        link.State.Should().Be(CardSyncLinkState.Linked);
        link.UpdatedAt.Should().Be(T1);
    }

    [Fact(DisplayName = "MarkStub переводит в stub и двигает метку времени")]
    public void MarkStub_sets_stub()
    {
        var link = NewLink();

        link.MarkStub(T1);

        link.State.Should().Be(CardSyncLinkState.Stub);
        link.IsStub.Should().BeTrue();
        link.UpdatedAt.Should().Be(T1);
    }

    [Fact(DisplayName = "MarkStub идемпотентен: повторный вызов не двигает метку")]
    public void MarkStub_idempotent()
    {
        var link = NewLink();
        link.MarkStub(T1);

        link.MarkStub(T2);

        link.UpdatedAt.Should().Be(T1);
    }

    [Theory(DisplayName = "Create отклоняет пустой intentId")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_intent(string intentId)
    {
        var act = () => CardSyncLink.Create(intentId, Card, Snap0, Cursors0, T0);

        act.Should().Throw<ArgumentException>();
    }
}
