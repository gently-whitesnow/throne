using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Tests.Intents;

public class ListIntentQaHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ListIntentQa возвращает QA пары когда intent существует")]
    public async Task Returns_qa_when_intent_exists()
    {
        var intents = Substitute.For<IIntentRepository>();
        var training = Substitute.For<IIntentTrainingRepository>();
        var id = IntentId.New();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(id, "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var qa = IntentQa.Create(
            "qa-1", id, intentVersionAtWrite: 1,
            question: "q?", answer: "a", now: Now, createdBy: IntentTrainingAuthor.Agent);
        training.ListQaByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([qa]);

        var handler = new ListIntentQaHandler(intents, training);

        var result = await handler.HandleAsync(new ListIntentQaQuery(id.Value), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Question.Should().Be("q?");
    }

    [Fact(DisplayName = "ListIntentQa кидает intent.not_found если intent отсутствует")]
    public async Task Throws_when_intent_missing()
    {
        var intents = Substitute.For<IIntentRepository>();
        var training = Substitute.For<IIntentTrainingRepository>();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns((Intent?)null);

        var handler = new ListIntentQaHandler(intents, training);

        var act = () => handler.HandleAsync(new ListIntentQaQuery("missing"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
        await training.DidNotReceive().ListQaByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>());
    }
}
