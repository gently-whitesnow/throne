using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using static Throne.Application.Tests.Repositories.RepositoryBindingTestData;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// «Синхронизировать ветку»: hard-syncs the local clone's current branch to its remote tip.
/// Distinct from «Обновить» — it is the only action that touches the working tree, requires
/// a ready clone with the folder on disk, and surfaces git failures to the caller.
/// </summary>
public class RepositoryBindingServiceSyncBranchTests
{
    [Fact(DisplayName = "SyncBranch: ready + папка есть → git-синхронизация по живому пути клона")]
    public async Task SyncBranch_ready_runs_git_sync_against_resolved_path()
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = true;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var result = await fixture.SyncBranchUseCase.ExecuteAsync(
            new SyncRepositoryBranchCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        result.Should().BeSameAs(binding);
        await fixture.WorkspaceSync.Received(1).SyncCurrentBranchToRemoteAsync(
            $"{WorkspaceRoot}/intents/{IntentIdValue}/octo__hello", Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "SyncBranch: clone не в ready → 409 not_ready, git не дёргается")]
    [InlineData(CloneStatusNames.Pending)]
    [InlineData(CloneStatusNames.Cloning)]
    [InlineData(CloneStatusNames.Failed)]
    [InlineData(CloneStatusNames.Broken)]
    public async Task SyncBranch_not_ready_throws(string cloneStatus)
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = true;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: cloneStatus);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var act = () => fixture.SyncBranchUseCase.ExecuteAsync(
            new SyncRepositoryBranchCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryNotReady);
        await fixture.WorkspaceSync.DidNotReceive().SyncCurrentBranchToRemoteAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "SyncBranch: ready, но папки нет на диске → 409 not_ready")]
    public async Task SyncBranch_ready_missing_folder_throws()
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = false;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var act = () => fixture.SyncBranchUseCase.ExecuteAsync(
            new SyncRepositoryBranchCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryNotReady);
        await fixture.WorkspaceSync.DidNotReceive().SyncCurrentBranchToRemoteAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "SyncBranch: git упал → GitProviderException мапится в branch_sync_failed")]
    public async Task SyncBranch_git_failure_maps_to_api_exception()
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = true;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.WorkspaceSync.SyncCurrentBranchToRemoteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new GitProviderException(
                GitProviderErrorKind.CliFailure, "git reset failed", "fatal: ...")));

        var act = () => fixture.SyncBranchUseCase.ExecuteAsync(
            new SyncRepositoryBranchCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryBranchSyncFailed);
    }

    [Fact(DisplayName = "SyncBranch на чужой intent_id → not_found (cross-tenant guard)")]
    public async Task SyncBranch_cross_intent_throws_not_found()
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: "other-intent", cloneStatus: CloneStatusNames.Ready);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var act = () => fixture.SyncBranchUseCase.ExecuteAsync(
            new SyncRepositoryBranchCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryBindingNotFound);
    }
}
