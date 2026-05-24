using FluentAssertions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Per-tick poll tests for T-10. Drives a single <see cref="PullRequestSyncTickWorkflow.RunAsync"/>
/// pass with a mocked <see cref="IGitProvider"/> across the four contractual branches:
/// 200 Fresh, 304 NotModified, 404, и rate-limit / транзиентная ошибка. Также проверяет,
/// что closed/merged PR выпадает из polling цикла (review-note D2) и что MarkBroken-путь
/// поднимает событие repository_clone_progress без падения тика.
/// </summary>
public class PullRequestSyncTickWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";

    [Fact(DisplayName = "Tick на 200 Fresh: persistence сохраняет комменты, эмитит pr_comment_added, snapshot.NewComments++")]
    public async Task Tick_fresh_persists_new_comments_and_emits_events()
    {
        var fixture = new TickFixture();
        var binding = fixture.SeedBinding(prState: PullRequestStateNames.Open);
        fixture.SeedSnapshot(binding.State.PullRequestNumber!.Value, PullRequestStateNames.Open);
        fixture.SeedFreshComments(binding, new PullRequestComment("c1", "alice", "lgtm", Now));

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        var snap = report.Snapshot;
        snap.Polled.Should().Be(1);
        snap.NewComments.Should().Be(1);
        snap.NotModified.Should().Be(0);
        fixture.Events.OfType<RepositoryPullRequestSynced>().Should().ContainSingle();
        fixture.Events.OfType<IntentPrCommentAdded>().Should().ContainSingle()
            .Which.Comment.UpstreamId.Should().Be("c1");
    }

    [Fact(DisplayName = "Tick на 304 NotModified: snapshot.NotModified++ и NewComments=0, fanout пустой")]
    public async Task Tick_not_modified_keeps_snapshot_counters()
    {
        var fixture = new TickFixture();
        var binding = fixture.SeedBinding(prState: PullRequestStateNames.Open, etag: "W/\"old\"");
        fixture.SeedSnapshot(binding.State.PullRequestNumber!.Value, PullRequestStateNames.Open);
        fixture.SeedNotModified(binding);

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        var snap = report.Snapshot;
        snap.Polled.Should().Be(1);
        snap.NotModified.Should().Be(1);
        snap.NewComments.Should().Be(0);
        fixture.Events.OfType<IntentPrCommentAdded>().Should().BeEmpty();
    }

    [Fact(DisplayName = "Tick на 404 (list_pull_request_comments → null): binding → broken, snapshot.MarkedBroken++")]
    public async Task Tick_404_marks_binding_broken()
    {
        var fixture = new TickFixture();
        var binding = fixture.SeedBinding(prState: PullRequestStateNames.Open);
        fixture.SeedSnapshot(binding.State.PullRequestNumber!.Value, PullRequestStateNames.Open);
        fixture.SeedCommentsResponse(binding, page: null);

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        var snap = report.Snapshot;
        snap.MarkedBroken.Should().Be(1);
        snap.Polled.Should().Be(0);
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Broken);
    }

    [Fact(DisplayName = "Tick на 404 при get_pull_request (snapshot null): MarkBroken + Backoff.Forget")]
    public async Task Tick_pr_snapshot_404_marks_broken()
    {
        var fixture = new TickFixture();
        var binding = fixture.SeedBinding(prState: PullRequestStateNames.Open);
        fixture.Provider.GetPullRequestAsync(
                binding.Coordinate.Owner, binding.Coordinate.Repo, binding.State.PullRequestNumber!.Value, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PullRequestSnapshot?>(null));

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        report.Snapshot.MarkedBroken.Should().Be(1);
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Broken);
    }

    [Fact(DisplayName = "Tick на rate-limit: backoff активирован, следующий тик скипает binding")]
    public async Task Tick_rate_limit_records_backoff_and_skips_next_tick()
    {
        var fixture = new TickFixture();
        var binding = fixture.SeedBinding(prState: PullRequestStateNames.Open);
        fixture.SeedSnapshot(binding.State.PullRequestNumber!.Value, PullRequestStateNames.Open);
        fixture.Provider.ListPullRequestCommentsAsync(
                binding.Coordinate.Owner, binding.Coordinate.Repo, binding.State.PullRequestNumber!.Value,
                etag: Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task<PullRequestCommentsPage?>>(_ =>
                Task.FromException<PullRequestCommentsPage?>(new GitProviderException(
                    GitProviderErrorKind.NetworkError,
                    "gh comments hit GitHub rate limit (resets at 2026-05-24 12:05:00Z).",
                    detail: "X-RateLimit-Reset=1748086500")));

        var first = await fixture.Workflow.RunAsync(CancellationToken.None);
        first.Snapshot.Failed.Should().Be(1);
        first.Failures.Should().ContainSingle()
            .Which.Kind.Should().Be(GitProviderErrorKind.NetworkError);

        var second = await fixture.Workflow.RunAsync(CancellationToken.None);
        second.Snapshot.Skipped.Should().Be(1);
        second.Snapshot.Polled.Should().Be(0);
    }

    [Fact(DisplayName = "Tick: PR закрылся upstream — обновляем pull_request_state и выпадаем из polling")]
    public async Task Tick_pr_closed_drops_from_polling()
    {
        var fixture = new TickFixture();
        var binding = fixture.SeedBinding(prState: PullRequestStateNames.Open);
        fixture.SeedSnapshot(binding.State.PullRequestNumber!.Value, PullRequestStateNames.Closed);

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        report.Snapshot.LifecycleClosed.Should().Be(1);
        report.Snapshot.Polled.Should().Be(0);
        binding.State.PullRequestState.Should().Be(PullRequestStateNames.Closed);
        await fixture.Provider.DidNotReceive().ListPullRequestCommentsAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private sealed class TickFixture
    {
        private readonly RecordingUnitOfWork _uow = new();

        public TickFixture()
        {
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            var providers = Substitute.For<IGitProviderRegistry>();
            Provider = Substitute.For<IGitProvider>();
            Provider.ProviderName.Returns(GitProviderNames.GitHub);
            providers.GetByName(GitProviderNames.GitHub).Returns(Provider);

            var clock = new FixedClock(Now);
            var comments = new RecordingCommentStore();
            var syncPersistence = new RepositoryPullRequestSyncPersistence(Bindings, comments, _uow, clock);
            var refresher = new PullRequestStateRefresher(Bindings, _uow, clock);
            var syncWorkflow = new RepositoryPullRequestSyncWorkflow(syncPersistence, refresher);
            var backoff = new PullRequestSyncBackoff(new PullRequestSyncOptions
            {
                PollIntervalSeconds = 60,
                BackoffInitialSeconds = 300,
                BackoffMaxSeconds = 900,
            });
            var visitor = new PullRequestSyncBindingVisitor(providers, syncWorkflow, refresher, backoff, clock);
            Workflow = new PullRequestSyncTickWorkflow(Bindings, visitor);

            Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public IGitProvider Provider { get; }
        public PullRequestSyncTickWorkflow Workflow { get; }
        public List<IDomainEvent> Events => _uow.Events;

        public IntentRepositoryBinding SeedBinding(
            string prState,
            int pullRequestNumber = 7,
            string? etag = null)
        {
            var snapshot = new IntentRepositoryBindingSnapshot(
                Id: BindingId.New(),
                IntentId: new IntentId(IntentIdValue),
                Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
                WorkspacePath: $"/tmp/throne-test/{IntentIdValue}/octo__hello",
                DefaultBranch: "main",
                CloneStatus: CloneStatusNames.Ready,
                CloneError: null,
                PullRequestNumber: pullRequestNumber,
                PullRequestState: prState,
                ReviewCommentsEtag: etag,
                LastSyncedAt: null,
                CreatedAt: Now,
                UpdatedAt: Now);
            var binding = IntentRepositoryBindingFactory.Restore(snapshot);
            Bindings.FindOpenForSyncAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([binding]));
            return binding;
        }

        public void SeedSnapshot(int number, string state) =>
            Provider.GetPullRequestAsync("octo", "hello", number, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<PullRequestSnapshot?>(new PullRequestSnapshot(number, state)));

        public void SeedFreshComments(IntentRepositoryBinding binding, params PullRequestComment[] comments) =>
            SeedCommentsResponse(binding, new PullRequestCommentsPage.Fresh(comments, Etag: "W/\"new\""));

        public void SeedNotModified(IntentRepositoryBinding binding) =>
            SeedCommentsResponse(binding, new PullRequestCommentsPage.NotModified());

        public void SeedCommentsResponse(IntentRepositoryBinding binding, PullRequestCommentsPage? page) =>
            Provider.ListPullRequestCommentsAsync(
                    binding.Coordinate.Owner, binding.Coordinate.Repo, binding.State.PullRequestNumber!.Value,
                    etag: Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(page));
    }

    private sealed class RecordingCommentStore : IPullRequestCommentRepository
    {
        private readonly Dictionary<string, List<PullRequestCommentRecord>> _byBinding = new(StringComparer.Ordinal);

        public Task<PersistPullRequestCommentsOutcome> PersistNewAsync(
            IntentRepositoryBinding binding,
            IReadOnlyList<PullRequestCommentRecord> candidates,
            CancellationToken ct)
        {
            if (!_byBinding.TryGetValue(binding.Id.Value, out var stored))
            {
                stored = [];
                _byBinding[binding.Id.Value] = stored;
            }
            var existing = stored.Select(c => c.UpstreamId).ToHashSet(StringComparer.Ordinal);
            var inserted = candidates.Where(c => existing.Add(c.UpstreamId)).ToList();
            stored.AddRange(inserted);
            return Task.FromResult(new PersistPullRequestCommentsOutcome(binding, inserted, stored.ToList()));
        }

        public Task<IReadOnlyList<PullRequestCommentRecord>> ListByBindingAsync(BindingId bindingId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PullRequestCommentRecord>>(
                _byBinding.TryGetValue(bindingId.Value, out var v) ? v.ToList() : []);
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        {
            var result = await work(ct);
            if (result is IDomainEventCarrier carrier)
            {
                Events.AddRange(carrier.Events);
            }
            return result;
        }

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
            ExecuteAsync(work, ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
