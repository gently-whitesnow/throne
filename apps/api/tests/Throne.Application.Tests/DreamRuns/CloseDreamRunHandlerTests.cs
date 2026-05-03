using FluentAssertions;
using NSubstitute;
using Throne.Application.DreamRuns;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class CloseDreamRunHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Closed outcome возвращает DreamRun")]
    public async Task Closed_returns_run()
    {
        var run = SampleRun();
        var (handler, repo) = NewHandler();
        repo.CloseAsync(default, default, default, default)
            .ReturnsForAnyArgs(new CloseDreamRunOutcome.Closed(run));

        var result = await handler.HandleAsync(new CloseDreamRunCommand(run.Id.Value, ReleaseEvidence: null), CancellationToken.None);

        result.Should().BeSameAs(run);
    }

    [Fact(DisplayName = "NotFound → ApiException(dream.run.not_found)")]
    public async Task NotFound_throws()
    {
        var (handler, repo) = NewHandler();
        repo.CloseAsync(default, default, default, default)
            .ReturnsForAnyArgs(new CloseDreamRunOutcome.NotFound());

        var act = () => handler.HandleAsync(new CloseDreamRunCommand("missing", null), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.DreamRunNotFound);
    }

    [Fact(DisplayName = "AlreadyClosed → ApiException(dream.run.already_closed)")]
    public async Task AlreadyClosed_throws_conflict()
    {
        var (handler, repo) = NewHandler();
        repo.CloseAsync(default, default, default, default)
            .ReturnsForAnyArgs(new CloseDreamRunOutcome.AlreadyClosed());

        var act = () => handler.HandleAsync(new CloseDreamRunCommand("id", null), CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.DreamRunAlreadyClosed);
    }

    private static (CloseDreamRunHandler Handler, IDreamRunRepository Repo) NewHandler()
    {
        var repo = Substitute.For<IDreamRunRepository>();
        var handler = new CloseDreamRunHandler(repo, new PassthroughUnitOfWork(), new FakeTimeProvider(Now));
        return (handler, repo);
    }

    private static DreamRun SampleRun() => DreamRun.Create(
        DreamRunId.New(),
        Now.AddDays(-1),
        Now.AddMinutes(-30),
        tokenCount: 50,
        [IntentRef.Create("intent-1", 50, Now)],
        Now);

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
