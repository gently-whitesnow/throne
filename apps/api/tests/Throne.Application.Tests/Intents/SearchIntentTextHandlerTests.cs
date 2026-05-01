using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Intents;

public class SearchIntentTextHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "00000000000000000000000000000001";

    [Fact(DisplayName = "Search возвращает совпадения и не выставляет TotalMatchesEstimate, если все влезли")]
    public async Task Returns_matches_without_estimate_when_under_limit()
    {
        var intent = Intent.Restore(new IntentId(IntentIdValue), "alpha\nbeta\ngamma", currentVersion: 1, [], Now, Now);
        var handler = NewHandler(out var repo);
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns(intent);

        var result = await handler.HandleAsync(
            new SearchIntentTextQuery(IntentIdValue, Query: "beta", ContextLines: 0, Limit: 10),
            CancellationToken.None);

        result.Matches.Should().HaveCount(1);
        result.TotalMatchesEstimate.Should().BeNull();
    }

    [Fact(DisplayName = "Search выставляет TotalMatchesEstimate, если число совпадений > limit")]
    public async Task Sets_estimate_when_total_exceeds_limit()
    {
        var intent = Intent.Restore(new IntentId(IntentIdValue), "x\nx\nx\nx\nx", currentVersion: 1, [], Now, Now);
        var handler = NewHandler(out var repo);
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns(intent);

        var result = await handler.HandleAsync(
            new SearchIntentTextQuery(IntentIdValue, Query: "x", ContextLines: 0, Limit: 2),
            CancellationToken.None);

        result.Matches.Should().HaveCount(2);
        result.TotalMatchesEstimate.Should().Be(5);
    }

    [Fact(DisplayName = "Search → ApiException(intent.not_found), если Intent не найден")]
    public async Task Missing_intent_throws_not_found()
    {
        var handler = NewHandler(out var repo);
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns((Intent?)null);

        var act = () => handler.HandleAsync(
            new SearchIntentTextQuery(IntentIdValue, "foo", null, null),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "Empty query → ApiException(validation.failed)")]
    public async Task Empty_query_fails_validation()
    {
        var handler = NewHandler(out _);

        var act = () => handler.HandleAsync(
            new SearchIntentTextQuery(IntentIdValue, string.Empty, null, null),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static SearchIntentTextHandler NewHandler(out IIntentRepository repo)
    {
        repo = Substitute.For<IIntentRepository>();
        return new SearchIntentTextHandler(repo);
    }
}
