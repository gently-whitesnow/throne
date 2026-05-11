using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Domain.Tests.Intents.Linking;

public class IntentLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create нормализует пустой rationale в null")]
    public void Empty_rationale_normalizes_to_null()
    {
        var link = IntentLink.Create(
            id: "id-1",
            fromId: new IntentId("a"),
            toId: new IntentId("b"),
            type: IntentLinkType.Relates,
            author: IntentLinkAuthor.User,
            rationale: "   ",
            createdAt: Now);

        link.Rationale.Should().BeNull();
    }

    [Fact(DisplayName = "Create запрещает self-link")]
    public void Self_link_throws()
    {
        var act = () => IntentLink.Create(
            id: "id-1",
            fromId: new IntentId("same"),
            toId: new IntentId("same"),
            type: IntentLinkType.Relates,
            author: IntentLinkAuthor.User,
            rationale: null,
            createdAt: Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Create отвергает неизвестный тип")]
    public void Unknown_type_throws()
    {
        var act = () => IntentLink.Create(
            id: "id-1",
            fromId: new IntentId("a"),
            toId: new IntentId("b"),
            type: "supersedes",
            author: IntentLinkAuthor.User,
            rationale: null,
            createdAt: Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "IsSupportedStage1: stage-1 типы — да, duplicate_of — нет")]
    [InlineData(IntentLinkType.Relates, true)]
    [InlineData(IntentLinkType.Blocks, true)]
    [InlineData(IntentLinkType.DerivedFrom, true)]
    [InlineData(IntentLinkType.DuplicateOf, false)]
    public void Stage1_supported_types(string type, bool supported)
    {
        IntentLinkType.IsSupportedStage1(type).Should().Be(supported);
    }
}
