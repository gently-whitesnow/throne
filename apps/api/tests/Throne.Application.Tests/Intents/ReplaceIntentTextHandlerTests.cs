using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Application.Tests.Intents;

public class ReplaceIntentTextHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "00000000000000000000000000000001";
    private static readonly int[] ExpectedMatchLines = [1, 3, 4];

    [Fact(DisplayName = "ReplaceIntentText возвращает Intent при Replaced outcome")]
    public async Task Replaced_returns_intent()
    {
        var existing = Intent.Restore(
            new IntentId(IntentIdValue),
            "hello there",
            IntentStatusNames.Work,
            currentVersion: 2,
            [],
            Now,
            Now);
        var handler = NewHandler(out var repo);
        repo.ReplaceTextAsync(
                Arg.Any<IntentId>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<TextVersionAuthor>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new ReplaceIntentTextOutcome.Replaced(existing));

        var result = await handler.HandleAsync(
            new ReplaceIntentTextCommand(IntentIdValue, ExpectedVersion: 1, OldText: "world", NewText: "there", Author: TextVersionAuthor.Agent),
            CancellationToken.None);

        result.Should().BeSameAs(existing);
    }

    [Fact(DisplayName = "NotFound → ApiException(intent.not_found)")]
    public async Task NotFound_throws_intent_not_found()
    {
        var handler = NewHandler(out var repo);
        repo.ReplaceTextAsync(default, default, default!, default!, default, default, default)
            .ReturnsForAnyArgs(new ReplaceIntentTextOutcome.NotFound());

        var act = () => handler.HandleAsync(
            new ReplaceIntentTextCommand(IntentIdValue, 1, "world", "there", TextVersionAuthor.Agent),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentNotFound);
        ex.Extensions["intent_id"].Should().Be(IntentIdValue);
    }

    [Fact(DisplayName = "VersionConflict → ApiException(intent.version_conflict) c expected/current")]
    public async Task VersionConflict_throws_with_version_extensions()
    {
        var handler = NewHandler(out var repo);
        repo.ReplaceTextAsync(default, default, default!, default!, default, default, default)
            .ReturnsForAnyArgs(new ReplaceIntentTextOutcome.VersionConflict(CurrentVersion: 5));

        var act = () => handler.HandleAsync(
            new ReplaceIntentTextCommand(IntentIdValue, 3, "world", "there", TextVersionAuthor.Agent),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentVersionConflict);
        ex.Extensions["expected_version"].Should().Be(3);
        ex.Extensions["current_version"].Should().Be(5);
    }

    [Fact(DisplayName = "MatchNotFound → ApiException c query_preview и hint")]
    public async Task MatchNotFound_throws_with_preview()
    {
        var handler = NewHandler(out var repo);
        repo.ReplaceTextAsync(default, default, default!, default!, default, default, default)
            .ReturnsForAnyArgs(new ReplaceIntentTextOutcome.MatchNotFound("xyz"));

        var act = () => handler.HandleAsync(
            new ReplaceIntentTextCommand(IntentIdValue, 1, "xyz", "abc", TextVersionAuthor.Agent),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentTextMatchNotFound);
        ex.Extensions["query_preview"].Should().Be("xyz");
        ex.Extensions.Should().ContainKey("hint");
    }

    [Fact(DisplayName = "MatchAmbiguous → ApiException c matches_count и match_lines")]
    public async Task MatchAmbiguous_throws_with_matches_info()
    {
        var handler = NewHandler(out var repo);
        repo.ReplaceTextAsync(default, default, default!, default!, default, default, default)
            .ReturnsForAnyArgs(new ReplaceIntentTextOutcome.MatchAmbiguous(3, [1, 3, 4]));

        var act = () => handler.HandleAsync(
            new ReplaceIntentTextCommand(IntentIdValue, 1, "foo", "bar", TextVersionAuthor.Agent),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentTextMatchAmbiguous);
        ex.Extensions["matches_count"].Should().Be(3);
        ex.Extensions["match_lines"].Should().BeEquivalentTo(ExpectedMatchLines);
        ex.Extensions.Should().ContainKey("hint");
    }

    [Fact(DisplayName = "Empty old_text → ApiException(validation.failed)")]
    public async Task Empty_old_text_fails_validation()
    {
        var handler = NewHandler(out _);

        var act = () => handler.HandleAsync(
            new ReplaceIntentTextCommand(IntentIdValue, 1, "", "x", TextVersionAuthor.Agent),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static ReplaceIntentTextHandler NewHandler(out IIntentRepository repo)
    {
        repo = Substitute.For<IIntentRepository>();
        return new ReplaceIntentTextHandler(repo, new PassthroughUnitOfWork(), new FakeTimeProvider(Now));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
