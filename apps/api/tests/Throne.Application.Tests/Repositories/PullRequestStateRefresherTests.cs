using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class PullRequestStateRefresherTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";

    [Fact(DisplayName = "Refresh: уже merged PR повторно запускает auto-close интента")]
    public async Task Refresh_retries_auto_close_when_state_is_already_merged()
    {
        var fixture = new Fixture();
        var binding = Binding(PullRequestStateNames.Merged);
        fixture.SeedPullRequestSnapshot(binding, PullRequestStateNames.Merged);
        fixture.SeedSiblings(binding);
        fixture.SeedIntent(IntentStatusNames.AwaitingOperator);

        var refreshed = await fixture.Refresher.RefreshAsync(binding, fixture.Provider, CancellationToken.None);

        refreshed.Should().BeSameAs(binding);
        await fixture.Bindings.DidNotReceive().SaveAsync(
            Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>());
        await fixture.Intents.Received(1).SetStatusBySystemAsync(
            new IntentId(IntentIdValue),
            IntentStatusNames.Done,
            Arg.Is<string?>(r => r != null && r.Contains("#7", StringComparison.Ordinal)),
            IntentMergeAutoCloser.Source,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    private static IntentRepositoryBinding Binding(string? prState) =>
        IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(IntentIdValue),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            WorkspacePath: $"/tmp/throne-test/{IntentIdValue}/octo__hello",
            DefaultBranch: "main",
            CloneStatus: CloneStatusNames.Ready,
            CloneError: null,
            PullRequestNumber: 7,
            PullRequestState: prState,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now));

    private sealed class Fixture
    {
        public Fixture()
        {
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            Intents = Substitute.For<ISystemIntentStatusWriter>();
            Provider = Substitute.For<IGitProvider>();
            Refresher = new PullRequestStateRefresher(
                Bindings,
                new PassthroughUnitOfWork(),
                new IntentMergeAutoCloser(
                    Bindings,
                    Intents,
                    new PassthroughUnitOfWork(),
                    new FixedClock(Now),
                    NullLogger<IntentMergeAutoCloser>.Instance),
                new FixedClock(Now),
                NullLogger<PullRequestStateRefresher>.Instance);

            Intents.SetStatusBySystemAsync(
                    Arg.Any<IntentId>(),
                    Arg.Any<string>(),
                    Arg.Any<string?>(),
                    Arg.Any<string>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult<SetIntentStatusOutcome>(
                    new SetIntentStatusOutcome.Updated(
                        Intent.Restore(ci.Arg<IntentId>(), "x", ci.ArgAt<string>(1), 1, [], Now, Now))));
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public ISystemIntentStatusWriter Intents { get; }
        public IGitProvider Provider { get; }
        public PullRequestStateRefresher Refresher { get; }

        public void SeedPullRequestSnapshot(IntentRepositoryBinding binding, string state) =>
            Provider.GetPullRequestAsync(
                    binding.Coordinate.Owner,
                    binding.Coordinate.Repo,
                    binding.State.PullRequestNumber!.Value,
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<PullRequestSnapshot?>(new PullRequestSnapshot(
                    binding.State.PullRequestNumber.Value,
                    state)));

        public void SeedSiblings(params IntentRepositoryBinding[] bindings) =>
            Bindings.FindByIntentAsync(new IntentId(IntentIdValue), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(bindings));

        public void SeedIntent(string status) =>
            Intents.GetByIdForSystemAsync(new IntentId(IntentIdValue), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Intent?>(
                    Intent.Restore(new IntentId(IntentIdValue), "x", status, 1, [], Now, Now)));
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
