using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using static Throne.Application.Tests.Repositories.RepositoryBindingTestData;

namespace Throne.Application.Tests.Repositories;

public class RepositoryBindingServiceTests
{
    [Theory(DisplayName = "Bind создаёт binding, считает workspace_path и пушит в clone-queue по флагу enqueueClone")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Bind_persists_binding_and_enqueues_clone(bool enqueueClone)
    {
        var fixture = new ServiceFixture();
        fixture.IntentExists(IntentIdValue);
        fixture.ProviderAuthenticated();
        fixture.CreateReturnsCreated();

        var binding = await fixture.Service.BindAsync(
            new BindRepositoryCommand(IntentIdValue, GitProviderNames.GitHub, "octo", "hello", DefaultBranch: null, PullRequestNumber: 42),
            CancellationToken.None,
            enqueueClone: enqueueClone);

        binding.Coordinate.Provider.Should().Be(GitProviderNames.GitHub);
        binding.WorkspacePath.Should().Be($"{WorkspaceRoot}/intents/{IntentIdValue}/octo__hello");
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Pending);
        binding.State.PullRequestNumber.Should().Be(42);

        await fixture.Bindings.Received(1).CreateAsync(
            Arg.Is<IntentRepositoryBinding>(b => b.Coordinate.Owner == "octo" && b.Coordinate.Repo == "hello"),
            Arg.Any<CancellationToken>());
        // enqueueClone:false — авто-байнд Run pre-flight: клон ставит в очередь следующий шаг
        // (RunPreflightCloneScheduler), а не сам bind, иначе binding попадёт в очередь дважды.
        if (enqueueClone)
        {
            fixture.Queue.Enqueued.Should().ContainSingle().Which.Should().Be(binding.Id);
        }
        else
        {
            fixture.Queue.Enqueued.Should().BeEmpty();
        }
    }

    [Fact(DisplayName = "Bind на повторный (owner, repo) даёт 409 repository_binding.already_exists")]
    public async Task Bind_duplicate_throws_409()
    {
        var fixture = new ServiceFixture();
        fixture.IntentExists(IntentIdValue);
        fixture.ProviderAuthenticated();
        var existing = NewBinding(intentId: IntentIdValue, owner: "octo", repo: "hello");
        fixture.Bindings.CreateAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CreateBindingOutcome>(new CreateBindingOutcome.Duplicate(existing)));

        var act = () => fixture.Service.BindAsync(
            new BindRepositoryCommand(IntentIdValue, GitProviderNames.GitHub, "octo", "hello", DefaultBranch: null, PullRequestNumber: null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryBindingAlreadyExists);
        ex.Which.Extensions["binding_id"].Should().Be(existing.Id.Value);
        fixture.Queue.Enqueued.Should().BeEmpty();
    }

    [Fact(DisplayName = "Bind падает с repository.provider_not_authenticated если gh не залогинен")]
    public async Task Bind_provider_unauthenticated_throws()
    {
        var fixture = new ServiceFixture();
        fixture.IntentExists(IntentIdValue);
        fixture.Provider.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProviderAuthStatus(GitProviderNames.GitHub, IsAuthenticated: false, Detail: "run gh auth login")));

        var act = () => fixture.Service.BindAsync(
            new BindRepositoryCommand(IntentIdValue, GitProviderNames.GitHub, "octo", "hello", DefaultBranch: null, PullRequestNumber: null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryProviderNotAuthenticated);
        await fixture.Bindings.DidNotReceive().CreateAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>());
        fixture.Queue.Enqueued.Should().BeEmpty();
    }

    [Fact(DisplayName = "Bind с неизвестным провайдером даёт repository.provider_unsupported")]
    public async Task Bind_unknown_provider_throws()
    {
        var fixture = new ServiceFixture();
        fixture.IntentExists(IntentIdValue);
        fixture.Providers.GetByName("bitbucket").Returns((IGitProvider?)null);

        var command = new BindRepositoryCommand(
            IntentIdValue, "bitbucket", "octo", "hello", DefaultBranch: null, PullRequestNumber: null);
        var act = () => fixture.Service.BindAsync(command, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryProviderUnsupported);
    }

    [Fact(DisplayName = "Bind для несуществующего intent отдаёт intent.not_found")]
    public async Task Bind_missing_intent_throws()
    {
        var fixture = new ServiceFixture();
        fixture.Intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Intent?>(null));

        var act = () => fixture.Service.BindAsync(
            new BindRepositoryCommand("missing", GitProviderNames.GitHub, "octo", "hello", DefaultBranch: null, PullRequestNumber: null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "Unbind удаляет binding и поднимает IntentRepositoryUnbound")]
    public async Task Unbind_deletes_and_emits_event()
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: IntentIdValue);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.Bindings.DeleteAsync(binding.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DeleteBindingOutcome>(new DeleteBindingOutcome.Deleted(binding)));

        await fixture.Service.UnbindAsync(new UnbindRepositoryCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        await fixture.Bindings.Received(1).DeleteAsync(binding.Id, Arg.Any<CancellationToken>());
        fixture.Remover.Removed.Should().ContainSingle()
            .Which.Should().Be($"{WorkspaceRoot}/intents/{IntentIdValue}/octo__hello");
        var outcome = new DeleteBindingOutcome.Deleted(binding);
        outcome.Events.Should().ContainSingle().Which.Should().BeOfType<IntentRepositoryUnbound>();
    }

    [Fact(DisplayName = "Unbind на чужой intent_id не находит binding (cross-tenant guard)")]
    public async Task Unbind_cross_intent_throws_not_found()
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: "other-intent");
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var act = () => fixture.Service.UnbindAsync(
            new UnbindRepositoryCommand(IntentIdValue, binding.Id.Value),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryBindingNotFound);
    }

    [Fact(DisplayName = "ListByIntent делегирует в port и возвращает binding'и")]
    public async Task List_delegates_to_repository()
    {
        var fixture = new ServiceFixture();
        fixture.IntentExists(IntentIdValue);
        var bindings = new[] { NewBinding(intentId: IntentIdValue) };
        fixture.Bindings.FindByIntentAsync(Arg.Is<IntentId>(i => i.Value == IntentIdValue), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(bindings));

        var result = await fixture.Service.ListByIntentAsync(IntentIdValue, CancellationToken.None);

        result.Should().BeEquivalentTo(bindings);
    }

    [Fact(DisplayName = "SyncPullRequest возвращает свежие комменты и записывает etag/last_synced_at")]
    public async Task Sync_returns_fresh_comments_and_records_etag()
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 7);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));
        // Review D2: manual sync now refreshes pull_request_state first — stub the
        // upstream PR snapshot so the refresh succeeds before the comments fetch.
        fixture.Provider.GetPullRequestAsync("octo", "hello", 7, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PullRequestSnapshot?>(new PullRequestSnapshot(7, PullRequestStateNames.Open)));
        var fresh = new[] { new PullRequestComment("c1", "alice", "lgtm", Now) };
        fixture.Provider.ListPullRequestCommentsAsync(
                "octo", "hello", 7, etag: null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PullRequestCommentsPage?>(new PullRequestCommentsPage.Fresh(fresh, Etag: "W/\"abc\"")));

        var result = await fixture.Service.SyncPullRequestAsync(
            new SyncRepositoryPullRequestCommand(IntentIdValue, binding.Id.Value),
            CancellationToken.None);

        result.NotModified.Should().BeFalse();
        result.NewComments.Select(c => c.Id).Should().Equal("c1");
        result.AllStored.Select(c => c.Id).Should().Equal("c1");
        result.Binding.State.ReviewCommentsEtag.Should().Be("W/\"abc\"");
        result.Binding.State.LastSeenReviewCommentAt.Should().Be(Now);
        result.Binding.State.LastSyncedAt.Should().Be(Now);
        result.Events.OfType<RepositoryPullRequestSynced>().Should().ContainSingle()
            .Which.CommentCount.Should().Be(1);
        result.Events.OfType<IntentPrCommentAdded>().Should().ContainSingle()
            .Which.Comment.Id.Should().Be("c1");
    }

    [Fact(DisplayName = "SyncPullRequest на 304 не возвращает комментов и обновляет last_synced_at")]
    public async Task Sync_not_modified_keeps_existing_etag()
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 7, etag: "W/\"old\"");
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));
        // Review D2: manual sync refreshes pull_request_state first.
        fixture.Provider.GetPullRequestAsync("octo", "hello", 7, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PullRequestSnapshot?>(new PullRequestSnapshot(7, PullRequestStateNames.Open)));
        fixture.Provider.ListPullRequestCommentsAsync(
                "octo", "hello", 7, etag: "W/\"old\"", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PullRequestCommentsPage?>(new PullRequestCommentsPage.NotModified()));

        var result = await fixture.Service.SyncPullRequestAsync(
            new SyncRepositoryPullRequestCommand(IntentIdValue, binding.Id.Value),
            CancellationToken.None);

        result.NotModified.Should().BeTrue();
        result.NewComments.Should().BeEmpty();
        result.AllStored.Should().BeEmpty();
        result.Binding.State.ReviewCommentsEtag.Should().Be("W/\"old\"");
        result.Binding.State.LastSyncedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "SyncPullRequest на 404 переводит binding в broken и бросает repository.upstream_gone")]
    public async Task Sync_404_marks_broken_and_throws()
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 7);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));
        fixture.Provider.ListPullRequestCommentsAsync(
                "octo", "hello", 7, etag: null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PullRequestCommentsPage?>(null));

        var act = () => fixture.Service.SyncPullRequestAsync(
            new SyncRepositoryPullRequestCommand(IntentIdValue, binding.Id.Value),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryUpstreamGone);
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Broken);
        await fixture.Bindings.Received(1).SaveAsync(
            Arg.Is<IntentRepositoryBinding>(b => b.State.CloneStatus == CloneStatusNames.Broken),
            Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "SyncPullRequest валидирует pre-conditions binding'а перед вызовом провайдера")]
    [InlineData(CloneStatusNames.Pending, 7, ErrorCodes.RepositoryNotReady)]
    [InlineData(CloneStatusNames.Ready, null, ErrorCodes.RepositoryPullRequestNotAttached)]
    public async Task Sync_validates_binding_preconditions(string cloneStatus, int? pullRequestNumber, string expectedCode)
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: cloneStatus, pullRequestNumber: pullRequestNumber);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var act = () => fixture.Service.SyncPullRequestAsync(
            new SyncRepositoryPullRequestCommand(IntentIdValue, binding.Id.Value),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(expectedCode);
    }
}
