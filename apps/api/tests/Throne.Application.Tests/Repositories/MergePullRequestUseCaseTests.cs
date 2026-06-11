using FluentAssertions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Keep-session-on-merge (review workspace): clearing «автозавершение сессии после мержа»
/// must flag the binding to skip auto-close <b>before</b> the provider merge fires, while the
/// default path leaves the binding untouched so the current auto-close behaviour holds.
/// </summary>
public class MergePullRequestUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "cleanup_after_merge=false → флаг очистки false на интенте + биндинг suppress до мержа")]
    public async Task Flags_keep_session_and_disables_cleanup_before_merge_when_off()
    {
        var fixture = new Fixture();

        var result = await fixture.UseCase.MergeAsync(
            fixture.Binding,
            new MergePullRequestRequest(MergeStrategy.Merge, DeleteBranch: false),
            cleanupAfterMerge: false,
            CancellationToken.None);

        result.Merged.Should().BeTrue();
        fixture.Binding.State.SuppressMergeAutoClose.Should().BeTrue();
        await fixture.Intents.Received(1).SetCleanupLocalStateOnDoneAsync(
            fixture.Binding.IntentId, false, Now, Arg.Any<CancellationToken>());
        await fixture.Bindings.Received(1).SaveAsync(
            Arg.Is<IntentRepositoryBinding>(b => b.State.SuppressMergeAutoClose),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "cleanup_after_merge=true (default) → флаг очистки true на интенте, биндинг не трогаем")]
    public async Task Enables_cleanup_and_leaves_binding_untouched_when_on()
    {
        var fixture = new Fixture();

        await fixture.UseCase.MergeAsync(
            fixture.Binding,
            new MergePullRequestRequest(MergeStrategy.Squash, DeleteBranch: true),
            cleanupAfterMerge: true,
            CancellationToken.None);

        fixture.Binding.State.SuppressMergeAutoClose.Should().BeFalse();
        await fixture.Intents.Received(1).SetCleanupLocalStateOnDoneAsync(
            fixture.Binding.IntentId, true, Now, Arg.Any<CancellationToken>());
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

            Intents = Substitute.For<IIntentRepository>();

            UseCase = new MergePullRequestUseCase(
                registry, Bindings, Intents, new PassthroughUnitOfWork(), new FixedClock(Now));
        }

        public IIntentRepositoryBindingRepository Bindings { get; }

        public IIntentRepository Intents { get; }

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
