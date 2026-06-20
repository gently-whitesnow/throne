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
            blocking: false,
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
            blocking: false,
            author: IntentLinkAuthor.User,
            rationale: null,
            createdAt: Now);

        act.Should().Throw<ArgumentException>();
    }
}
