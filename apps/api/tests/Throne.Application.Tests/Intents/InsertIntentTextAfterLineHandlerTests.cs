using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Intents;

public class InsertIntentTextAfterLineHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "00000000000000000000000000000001";

    [Fact(DisplayName = "Inserted → возвращает Intent")]
    public async Task Inserted_returns_intent()
    {
        var intent = Intent.Restore(
            new IntentId(IntentIdValue),
            "a\nX\nb",
            IntentStatusNames.Work,
            currentVersion: 2,
            [],
            Now,
            Now);
        var handler = NewHandler(out var repo);
        repo.InsertTextAfterLineAsync(default, default, default, default!, default, default)
            .ReturnsForAnyArgs(new InsertIntentTextAfterLineOutcome.Inserted(intent));

        var result = await handler.HandleAsync(
            new InsertIntentTextAfterLineCommand(IntentIdValue, ExpectedVersion: 1, AfterLine: 1, InsertText: "X\n"),
            CancellationToken.None);

        result.Should().BeSameAs(intent);
    }

    [Fact(DisplayName = "NotFound → ApiException(intent.not_found)")]
    public async Task NotFound_throws()
    {
        var handler = NewHandler(out var repo);
        repo.InsertTextAfterLineAsync(default, default, default, default!, default, default)
            .ReturnsForAnyArgs(new InsertIntentTextAfterLineOutcome.NotFound());

        var act = () => handler.HandleAsync(
            new InsertIntentTextAfterLineCommand(IntentIdValue, 1, 0, "x"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "VersionConflict → ApiException(intent.version_conflict)")]
    public async Task VersionConflict_throws()
    {
        var handler = NewHandler(out var repo);
        repo.InsertTextAfterLineAsync(default, default, default, default!, default, default)
            .ReturnsForAnyArgs(new InsertIntentTextAfterLineOutcome.VersionConflict(7));

        var act = () => handler.HandleAsync(
            new InsertIntentTextAfterLineCommand(IntentIdValue, 3, 0, "x"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentVersionConflict);
        ex.Extensions["expected_version"].Should().Be(3);
        ex.Extensions["current_version"].Should().Be(7);
    }

    [Fact(DisplayName = "LineOutOfRange → ApiException(intent.text.line_out_of_range)")]
    public async Task LineOutOfRange_throws()
    {
        var handler = NewHandler(out var repo);
        repo.InsertTextAfterLineAsync(default, default, default, default!, default, default)
            .ReturnsForAnyArgs(new InsertIntentTextAfterLineOutcome.LineOutOfRange(TotalLines: 2, RequestedAfterLine: 9));

        var act = () => handler.HandleAsync(
            new InsertIntentTextAfterLineCommand(IntentIdValue, 1, 9, "x"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentTextLineOutOfRange);
        ex.Extensions["total_lines"].Should().Be(2);
        ex.Extensions["requested_after_line"].Should().Be(9);
    }

    [Fact(DisplayName = "Negative after_line → ApiException(validation.failed)")]
    public async Task Negative_after_line_fails_validation()
    {
        var handler = NewHandler(out _);

        var act = () => handler.HandleAsync(
            new InsertIntentTextAfterLineCommand(IntentIdValue, 1, -1, "x"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static InsertIntentTextAfterLineHandler NewHandler(out IIntentRepository repo)
    {
        repo = Substitute.For<IIntentRepository>();
        return new InsertIntentTextAfterLineHandler(repo, new PassthroughUnitOfWork(), new FakeTimeProvider(Now));
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
