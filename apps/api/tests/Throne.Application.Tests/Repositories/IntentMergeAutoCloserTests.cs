using FluentAssertions;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Auto-close-on-merge (intent spec B / Q6): an intent goes to <c>done</c> only when every
/// PR-bearing binding it owns is <c>merged</c>; bindings without a PR never block; terminal
/// intents are left alone.
/// </summary>
public class IntentMergeAutoCloserTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";

    [Fact(DisplayName = "Все PR-несущие биндинги merged + интент нетерминальный → перевод в done с источником pr_merge")]
    public async Task Closes_intent_when_all_pr_bindings_merged()
    {
        var fixture = new Fixture();
        var merged = Binding(pr: 7, prState: PullRequestStateNames.Merged);
        fixture.SeedSiblings(merged);
        fixture.SeedIntent(IntentStatusNames.Work);

        await fixture.Closer.OnBindingMergedAsync(merged, CancellationToken.None);

        await fixture.Intents.Received(1).SetStatusBySystemAsync(
            new IntentId(IntentIdValue),
            IntentStatusNames.Done,
            Arg.Is<string?>(r => r != null && r.Contains("#7", StringComparison.Ordinal)),
            IntentMergeAutoCloser.Source,
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Биндинг без PR не блокирует закрытие")]
    public async Task Binding_without_pr_does_not_block_close()
    {
        var fixture = new Fixture();
        var merged = Binding(pr: 7, prState: PullRequestStateNames.Merged);
        fixture.SeedSiblings(merged, Binding(pr: null, prState: null));
        fixture.SeedIntent(IntentStatusNames.ReadyForReview);

        await fixture.Closer.OnBindingMergedAsync(merged, CancellationToken.None);

        await fixture.Intents.Received(1).SetStatusBySystemAsync(
            Arg.Any<IntentId>(), IntentStatusNames.Done, Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Есть PR-несущий биндинг ещё не merged → не закрываем")]
    public async Task Does_not_close_when_a_pr_binding_still_open()
    {
        var fixture = new Fixture();
        var merged = Binding(pr: 7, prState: PullRequestStateNames.Merged);
        fixture.SeedSiblings(merged, Binding(pr: 9, prState: PullRequestStateNames.Open));
        fixture.SeedIntent(IntentStatusNames.Work);

        await fixture.Closer.OnBindingMergedAsync(merged, CancellationToken.None);

        await fixture.Intents.DidNotReceive().SetStatusBySystemAsync(
            Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Интент уже терминальный (reject) → не трогаем")]
    public async Task Does_not_reopen_terminal_intent()
    {
        var fixture = new Fixture();
        var merged = Binding(pr: 7, prState: PullRequestStateNames.Merged);
        fixture.SeedSiblings(merged);
        fixture.SeedIntent(IntentStatusNames.Reject);

        await fixture.Closer.OnBindingMergedAsync(merged, CancellationToken.None);

        await fixture.Intents.DidNotReceive().SetStatusBySystemAsync(
            Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private static IntentRepositoryBinding Binding(int? pr, string? prState) =>
        IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(IntentIdValue),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            WorkspacePath: $"/tmp/throne-test/{IntentIdValue}/octo__hello",
            DefaultBranch: "main",
            CloneStatus: CloneStatusNames.Ready,
            CloneError: null,
            PullRequestNumber: pr,
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
            Intents.SetStatusBySystemAsync(
                    Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(),
                    Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult<SetIntentStatusOutcome>(
                    new SetIntentStatusOutcome.Updated(
                        Intent.Restore(new IntentId(IntentIdValue), "x", IntentStatusNames.Done, 1, [], Now, Now))));
            Closer = new IntentMergeAutoCloser(Bindings, Intents, new PassthroughUnitOfWork(), new FixedClock(Now));
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public ISystemIntentStatusWriter Intents { get; }
        public IntentMergeAutoCloser Closer { get; }

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
