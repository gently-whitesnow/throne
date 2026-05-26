using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Tests.Intents;

public class GetIntentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "GetIntent возвращает full text и current_version")]
    public async Task GetIntent_returns_intent()
    {
        var repo = Substitute.For<IIntentRepository>();
        var id = IntentId.New();
        var tagId = TagId.New();
        repo.GetByIdAsync(Arg.Is<IntentId>(x => x.Value == id.Value), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(id, "user-1", "body", IntentStatusNames.Draft, currentVersion: 3, [tagId], Now, Now));

        var handler = new GetIntentHandler(repo);

        var intent = await handler.HandleAsync(new GetIntentQuery(id.Value), CancellationToken.None);

        intent.Id.Value.Should().Be(id.Value);
        intent.State.Text.Should().Be("body");
        intent.State.CurrentVersion.Should().Be(3);
        intent.TagIds.Should().Equal(tagId);
    }

    [Fact(DisplayName = "GetIntent кидает intent.not_found если документа нет")]
    public async Task GetIntent_throws_when_missing()
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns((Intent?)null);
        var handler = new GetIntentHandler(repo);

        var act = () => handler.HandleAsync(new GetIntentQuery("missing"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }
}
