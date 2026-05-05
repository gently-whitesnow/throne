using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Tests.Intents;

public class ListIntentReviewsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ListIntentReviews возвращает review-записи когда intent существует")]
    public async Task Returns_reviews_when_intent_exists()
    {
        var intents = Substitute.For<IIntentRepository>();
        var training = Substitute.For<IIntentTrainingRepository>();
        var id = IntentId.New();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(id, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var review = IntentReview.Create(
            "review-1", "user-1", id, intentVersionAtWrite: 1,
            note: "looks good", reason: "ok", now: Now, createdBy: IntentTrainingAuthor.User);
        training.ListReviewsByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([review]);

        var handler = new ListIntentReviewsHandler(intents, training);

        var result = await handler.HandleAsync(new ListIntentReviewsQuery(id.Value), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Note.Should().Be("looks good");
        result[0].Reason.Should().Be("ok");
    }

    [Fact(DisplayName = "ListIntentReviews кидает intent.not_found если intent отсутствует")]
    public async Task Throws_when_intent_missing()
    {
        var intents = Substitute.For<IIntentRepository>();
        var training = Substitute.For<IIntentTrainingRepository>();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns((Intent?)null);

        var handler = new ListIntentReviewsHandler(intents, training);

        var act = () => handler.HandleAsync(new ListIntentReviewsQuery("missing"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
        await training.DidNotReceive().ListReviewsByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>());
    }
}
