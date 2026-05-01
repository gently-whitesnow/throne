using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Domain.Tests.Intents;

public class IntentQaTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create заполняет все поля")]
    public void Create_populates_fields()
    {
        var id = "qa-1";
        var intentId = IntentId.New();

        var qa = IntentQa.Create(id, intentId, intentVersionAtWrite: 3,
            question: "?", answer: ".", now: Now, createdBy: IntentTrainingAuthor.Agent);

        qa.Id.Should().Be(id);
        qa.IntentId.Should().Be(intentId);
        qa.IntentVersionAtWrite.Should().Be(3);
        qa.Question.Should().Be("?");
        qa.Answer.Should().Be(".");
        qa.CreatedAt.Should().Be(Now);
        qa.CreatedBy.Should().Be(IntentTrainingAuthor.Agent);
    }

    [Fact(DisplayName = "Create отвергает пустые question/answer и version<1")]
    public void Create_validates_inputs()
    {
        var intentId = IntentId.New();

        Action emptyQ = () => IntentQa.Create("id", intentId, 1, "", "a", Now, IntentTrainingAuthor.Agent);
        emptyQ.Should().Throw<ArgumentException>().WithParameterName("question");

        Action emptyA = () => IntentQa.Create("id", intentId, 1, "q", "", Now, IntentTrainingAuthor.Agent);
        emptyA.Should().Throw<ArgumentException>().WithParameterName("answer");

        Action zeroVersion = () => IntentQa.Create("id", intentId, 0, "q", "a", Now, IntentTrainingAuthor.Agent);
        zeroVersion.Should().Throw<ArgumentOutOfRangeException>();
    }
}
