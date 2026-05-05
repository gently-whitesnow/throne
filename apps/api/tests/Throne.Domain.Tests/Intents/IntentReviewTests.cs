using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Domain.Tests.Intents;

public class IntentReviewTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create заполняет все поля")]
    public void Create_populates_fields()
    {
        var intentId = IntentId.New();

        var review = IntentReview.Create("rev-1", "user-1", intentId, intentVersionAtWrite: 4,
            note: "n", reason: "r", now: Now, createdBy: IntentTrainingAuthor.Agent);

        review.Id.Should().Be("rev-1");
        review.OwnerUserId.Should().Be("user-1");
        review.IntentId.Should().Be(intentId);
        review.IntentVersionAtWrite.Should().Be(4);
        review.Note.Should().Be("n");
        review.Reason.Should().Be("r");
        review.CreatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "Create отвергает пустые note/reason и version<1")]
    public void Create_validates_inputs()
    {
        var intentId = IntentId.New();

        Action emptyN = () => IntentReview.Create("id", "user-1", intentId, 1, "", "r", Now, IntentTrainingAuthor.Agent);
        emptyN.Should().Throw<ArgumentException>().WithParameterName("note");

        Action emptyR = () => IntentReview.Create("id", "user-1", intentId, 1, "n", "", Now, IntentTrainingAuthor.Agent);
        emptyR.Should().Throw<ArgumentException>().WithParameterName("reason");

        Action zeroVersion = () => IntentReview.Create("id", "user-1", intentId, 0, "n", "r", Now, IntentTrainingAuthor.Agent);
        zeroVersion.Should().Throw<ArgumentOutOfRangeException>();

        Action emptyOwner = () => IntentReview.Create("id", "", intentId, 1, "n", "r", Now, IntentTrainingAuthor.Agent);
        emptyOwner.Should().Throw<ArgumentException>().WithParameterName("ownerUserId");
    }
}
