using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Shared test data + builders for the <see cref="RepositoryBindingService"/> tests
/// (split across <c>RepositoryBindingServiceTests</c> and
/// <c>RepositoryBindingServiceRefreshTests</c>). Pull constants/builders in with
/// <c>using static</c> so the test bodies read unqualified.
/// </summary>
internal static class RepositoryBindingTestData
{
    public static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    public const string WorkspaceRoot = "/tmp/throne-test-workspaces";
    public const string IntentIdValue = "intent-1";

    // CreateAsync aggregates the ensure outcome's Events, so the stub must return a
    // non-null EnsureRepositoryOutcome for any coordinate.
    public static IRepositoryRegistry StubRegistry()
    {
        var registry = Substitute.For<IRepositoryRegistry>();
        registry
            .EnsureRepositoryAsync(Arg.Any<RepoCoordinate>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => new EnsureRepositoryOutcome.Existed(
                Repository.Create(RepositoryId.New(), ci.Arg<RepoCoordinate>(), ci.Arg<DateTimeOffset>())));
        return registry;
    }

    public static IntentRepositoryBinding NewBinding(
        string intentId,
        string owner = "octo",
        string repo = "hello",
        string cloneStatus = CloneStatusNames.Pending,
        int? pullRequestNumber = null,
        string? etag = null)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(intentId),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, owner, repo),
            WorkspacePath: $"{WorkspaceRoot}/intents/{intentId}/{owner}__{repo}",
            DefaultBranch: "main",
            CloneStatus: cloneStatus,
            CloneError: null,
            PullRequestNumber: pullRequestNumber,
            PullRequestState: pullRequestNumber is null ? null : PullRequestStateNames.Open,
            ReviewCommentsEtag: etag,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now);
        return IntentRepositoryBinding.Restore(snapshot);
    }
}

internal sealed class ServiceFixture
{
    public ServiceFixture()
    {
        Intents = Substitute.For<IIntentRepository>();
        Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
        Providers = Substitute.For<IGitProviderRegistry>();
        Provider = Substitute.For<IGitProvider>();
        Provider.ProviderName.Returns(GitProviderNames.GitHub);
        Providers.GetByName(GitProviderNames.GitHub).Returns(Provider);
        Remover = new RecordingWorkspaceRemover();
        Probe = new StubWorkspaceProbe();
        Queue = new RecordingCloneQueue();
        BranchReader = Substitute.For<ILocalGitBranchReader>();
        var workspace = new StubWorkspaceRoot(RepositoryBindingTestData.WorkspaceRoot);
        var unitOfWork = new PassthroughUnitOfWork();
        var clock = new FixedClock(RepositoryBindingTestData.Now);
        var resolver = new RepositoryBindingResolver(Intents, Bindings, Providers);
        var persistence = new RepositoryBindingPersistence(
            Bindings, RepositoryBindingTestData.StubRegistry(), unitOfWork, clock, workspace, Remover, Probe);
        var cloneWriter = new RepositoryCloneTransitionWriter(Bindings, unitOfWork, clock);
        var syncPersistence = new RepositoryPullRequestSyncPersistence(Bindings, unitOfWork, clock);
        var autoCloser = new IntentMergeAutoCloser(
            Bindings,
            Substitute.For<ISystemIntentStatusWriter>(),
            unitOfWork,
            clock,
            NullLogger<IntentMergeAutoCloser>.Instance);
        var stateRefresher = new PullRequestStateRefresher(
            Bindings, unitOfWork, autoCloser, clock, NullLogger<PullRequestStateRefresher>.Instance);
        var syncWorkflow = new RepositoryPullRequestSyncWorkflow(syncPersistence, stateRefresher);
        var autoBind = new PullRequestAutoBindWorkflow(
            Bindings, Providers, BranchReader, persistence,
            NullLogger<PullRequestAutoBindWorkflow>.Instance);
        Service = new RepositoryBindingService(
            resolver, persistence, syncWorkflow, cloneWriter, Queue, autoBind,
            NullLogger<RepositoryBindingService>.Instance);
    }

    public IIntentRepository Intents { get; }
    public IIntentRepositoryBindingRepository Bindings { get; }
    public IGitProviderRegistry Providers { get; }
    public IGitProvider Provider { get; }
    public RecordingWorkspaceRemover Remover { get; }
    public StubWorkspaceProbe Probe { get; }
    public RecordingCloneQueue Queue { get; }
    public ILocalGitBranchReader BranchReader { get; }
    public RepositoryBindingService Service { get; }
}

/// <summary>
/// Stub-arrangement helpers for <see cref="ServiceFixture"/>. Extensions rather than fixture
/// methods so the fixture's public surface stays within the maintainability budget; callers
/// still read as <c>fixture.IntentExists(...)</c>.
/// </summary>
internal static class ServiceFixtureExtensions
{
    public static void IntentExists(this ServiceFixture fixture, string intentIdValue)
    {
        var intent = Intent.Restore(
            new IntentId(intentIdValue), "x", IntentStatusNames.Work, 1, [],
            RepositoryBindingTestData.Now, RepositoryBindingTestData.Now);
        fixture.Intents.GetByIdAsync(Arg.Is<IntentId>(i => i.Value == intentIdValue), Arg.Any<CancellationToken>())
            .Returns(intent);
    }

    public static void ProviderAuthenticated(this ServiceFixture fixture) =>
        fixture.Provider.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProviderAuthStatus(
                GitProviderNames.GitHub, IsAuthenticated: true, Account: "octocat", Host: "github.com")));

    public static void CreateReturnsCreated(this ServiceFixture fixture) =>
        fixture.Bindings.CreateAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<CreateBindingOutcome>(
                new CreateBindingOutcome.Created(ci.Arg<IntentRepositoryBinding>())));
}

internal sealed class StubWorkspaceRoot(string root) : IWorkspaceRootProvider
{
    public string ResolvedRoot { get; } = root;
}

internal sealed class RecordingWorkspaceRemover : IWorkspaceDirectoryRemover
{
    public List<string> Removed { get; } = [];

    public Task RemoveAsync(string absolutePath, CancellationToken ct)
    {
        Removed.Add(absolutePath);
        return Task.CompletedTask;
    }

    public Task RemoveContentsAsync(string directoryPath, CancellationToken ct) => Task.CompletedTask;
}

internal sealed class StubWorkspaceProbe : IWorkspaceDirectoryProbe
{
    public bool DirectoryExists { get; set; } = true;

    public bool Exists(string absolutePath) => DirectoryExists;
}

internal sealed class RecordingCloneQueue : IRepositoryCloneRequests
{
    public List<BindingId> Enqueued { get; } = [];

    public ValueTask EnqueueAsync(BindingId bindingId, CancellationToken ct)
    {
        Enqueued.Add(bindingId);
        return ValueTask.CompletedTask;
    }
}

internal sealed class PassthroughUnitOfWork : IUnitOfWork
{
    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

    public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
