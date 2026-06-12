using FluentAssertions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Merge owns only the auto-close-on-merge decision (D2): <c>suppress_auto_close=true</c> must flag
/// the binding to skip auto-close <b>before</b> the provider merge fires, while the default path
/// leaves the binding untouched so the current auto-close behaviour holds. The teardown-on-done
/// gate is no longer written from here — it lives on the intent endpoint.
/// </summary>
public class MergePullRequestUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "suppress_auto_close=true → биндинг suppress до мержа")]
    public async Task Flags_suppress_auto_close_before_merge_when_set()
    {
        var fixture = new Fixture();

        var result = await fixture.UseCase.MergeAsync(
            fixture.Binding,
            new MergePullRequestRequest(MergeStrategy.Merge, DeleteBranch: false),
            suppressAutoClose: true,
            CancellationToken.None);

        result.Merged.Should().BeTrue();
        fixture.Binding.State.SuppressMergeAutoClose.Should().BeTrue();
        await fixture.Bindings.Received(1).SaveAsync(
            Arg.Is<IntentRepositoryBinding>(b => b.State.SuppressMergeAutoClose),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "suppress_auto_close=false (default) → биндинг не трогаем")]
    public async Task Leaves_binding_untouched_when_not_suppressed()
    {
        var fixture = new Fixture();

        await fixture.UseCase.MergeAsync(
            fixture.Binding,
            new MergePullRequestRequest(MergeStrategy.Squash, DeleteBranch: true),
            suppressAutoClose: false,
            CancellationToken.None);

        fixture.Binding.State.SuppressMergeAutoClose.Should().BeFalse();
        await fixture.Bindings.DidNotReceive().SaveAsync(
            Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>());
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult<SaveBindingOutcome>(
                    new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));

            var provider = Substitute.For<IGitProvider>();
            provider.MergePullRequestAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                    Arg.Any<MergePullRequestRequest>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new PullRequestMergeResult(true, PullRequestStateNames.Merged, null)));

            var registry = Substitute.For<IGitProviderRegistry>();
            registry.GetByName(GitProviderNames.GitHub).Returns(provider);

            UseCase = new MergePullRequestUseCase(
                registry, Bindings, new PassthroughUnitOfWork(), new FixedClock(Now));
        }

        public IIntentRepositoryBindingRepository Bindings { get; }

        public MergePullRequestUseCase UseCase { get; }

        public IntentRepositoryBinding Binding { get; } = IntentRepositoryBinding.Restore(
            new IntentRepositoryBindingSnapshot(
                Id: BindingId.New(),
                IntentId: new IntentId("intent-1"),
                Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
                WorkspacePath: "/tmp/throne-test/intent-1/octo__hello",
                DefaultBranch: "main",
                CloneStatus: CloneStatusNames.Ready,
                CloneError: null,
                PullRequestNumber: 7,
                PullRequestState: PullRequestStateNames.Open,
                ReviewCommentsEtag: null,
                LastSeenReviewCommentAt: null,
                LastSyncedAt: null,
                CreatedAt: Now,
                UpdatedAt: Now));
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
            work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
