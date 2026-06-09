using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class RepositoryBindingPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private const string WorkspaceRoot = "/tmp/throne-test-workspaces";
    private const string IntentIdValue = "intent-1";

    [Fact(DisplayName = "Delete сначала удаляет папку (по живому root), потом запись")]
    public async Task Delete_removes_directory_then_record()
    {
        var fixture = new Fixture();
        var binding = NewBinding();
        fixture.Bindings.DeleteAsync(binding.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DeleteBindingOutcome>(new DeleteBindingOutcome.Deleted(binding)));

        await fixture.Persistence.DeleteAsync(binding, CancellationToken.None);

        fixture.Remover.Removed.Should().ContainSingle()
            .Which.Should().Be($"{WorkspaceRoot}/intents/{IntentIdValue}/octo__hello");
        await fixture.Bindings.Received(1).DeleteAsync(binding.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Delete при ошибке удаления папки бросает workspace_removal_failed и не трогает запись")]
    public async Task Delete_directory_failure_keeps_record()
    {
        var fixture = new Fixture();
        var binding = NewBinding();
        fixture.Remover.Fail = new IOException("папка занята процессом");

        var act = () => fixture.Persistence.DeleteAsync(binding, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryWorkspaceRemovalFailed);
        await fixture.Bindings.DidNotReceive().DeleteAsync(Arg.Any<BindingId>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Create материализует Repository-реестр по координате binding'а")]
    public async Task Create_ensures_registry_for_coordinate()
    {
        var fixture = new Fixture();
        var binding = NewBinding();
        fixture.Bindings.CreateAsync(binding, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CreateBindingOutcome>(new CreateBindingOutcome.Created(binding)));
        fixture.Registry.EnsureRepositoryAsync(binding.Coordinate, Now, Arg.Any<CancellationToken>())
            .Returns(new EnsureRepositoryOutcome.Existed(
                Repository.Create(RepositoryId.New(), binding.Coordinate, Now)));

        await fixture.Persistence.CreateAsync(binding, CancellationToken.None);

        await fixture.Registry.Received(1)
            .EnsureRepositoryAsync(binding.Coordinate, Now, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Create с Duplicate не материализует Repository-реестр")]
    public async Task Create_duplicate_does_not_ensure_registry()
    {
        var fixture = new Fixture();
        var binding = NewBinding();
        fixture.Bindings.CreateAsync(binding, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CreateBindingOutcome>(new CreateBindingOutcome.Duplicate(binding)));

        var act = () => fixture.Persistence.CreateAsync(binding, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryBindingAlreadyExists);
        await fixture.Registry.DidNotReceive()
            .EnsureRepositoryAsync(Arg.Any<RepoCoordinate>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private static IntentRepositoryBinding NewBinding()
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(IntentIdValue),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            WorkspacePath: $"{WorkspaceRoot}/intents/{IntentIdValue}/octo__hello",
            DefaultBranch: "main",
            CloneStatus: CloneStatusNames.Ready,
            CloneError: null,
            PullRequestNumber: null,
            PullRequestState: null,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now);
        return IntentRepositoryBinding.Restore(snapshot);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            Registry = Substitute.For<IRepositoryRegistry>();
            Remover = new FailableWorkspaceRemover();
            Persistence = new RepositoryBindingPersistence(
                Bindings,
                Registry,
                new PassthroughUnitOfWork(),
                new FixedClock(Now),
                new StubWorkspaceRoot(WorkspaceRoot),
                Remover,
                Substitute.For<IWorkspaceDirectoryProbe>());
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public IRepositoryRegistry Registry { get; }
        public FailableWorkspaceRemover Remover { get; }
        public RepositoryBindingPersistence Persistence { get; }
    }

    private sealed class FailableWorkspaceRemover : IWorkspaceDirectoryRemover
    {
        public List<string> Removed { get; } = [];
        public Exception? Fail { get; set; }

        public Task RemoveAsync(string absolutePath, CancellationToken ct)
        {
            if (Fail is not null)
            {
                return Task.FromException(Fail);
            }

            Removed.Add(absolutePath);
            return Task.CompletedTask;
        }

        public Task RemoveContentsAsync(string directoryPath, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class StubWorkspaceRoot(string root) : IWorkspaceRootProvider
    {
        public string ResolvedRoot { get; } = root;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
