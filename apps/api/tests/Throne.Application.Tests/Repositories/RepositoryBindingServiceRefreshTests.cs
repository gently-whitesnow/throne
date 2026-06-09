using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using static Throne.Application.Tests.Repositories.RepositoryBindingTestData;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// «Обновить» disk-recovery (ADR-0024): the trigger is the on-disk folder, the Mongo
/// <c>clone_status</c> is ignored. Folder gone → re-queue the clone in <c>pending</c>;
/// folder present → no-op. Shares <see cref="ServiceFixture"/> with the other binding-service tests.
/// </summary>
public class RepositoryBindingServiceRefreshTests
{
    [Theory(DisplayName = "Refresh: папки нет → binding в pending и enqueue в clone-очередь (статус Mongo игнорируем)")]
    [InlineData(CloneStatusNames.Ready)]
    [InlineData(CloneStatusNames.Failed)]
    [InlineData(CloneStatusNames.Broken)]
    public async Task Refresh_missing_folder_requeues_clone(string cloneStatus)
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = false;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: cloneStatus);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));

        var result = await fixture.Service.RefreshAsync(
            new RefreshRepositoryBindingCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        result.State.CloneStatus.Should().Be(CloneStatusNames.Pending);
        result.State.CloneError.Should().BeNull();
        fixture.Queue.Enqueued.Should().ContainSingle().Which.Should().Be(binding.Id);
        await fixture.Bindings.Received(1).SaveAsync(
            Arg.Is<IntentRepositoryBinding>(b => b.State.CloneStatus == CloneStatusNames.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Refresh: папки нет, binding уже pending → только enqueue без повторного перехода")]
    public async Task Refresh_missing_folder_already_pending_only_enqueues()
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = false;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Pending);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var result = await fixture.Service.RefreshAsync(
            new RefreshRepositoryBindingCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        result.State.CloneStatus.Should().Be(CloneStatusNames.Pending);
        fixture.Queue.Enqueued.Should().ContainSingle().Which.Should().Be(binding.Id);
        await fixture.Bindings.DidNotReceive().SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Refresh: папка есть → no-op, binding возвращается без изменений и без enqueue")]
    public async Task Refresh_existing_folder_is_noop()
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = true;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var result = await fixture.Service.RefreshAsync(
            new RefreshRepositoryBindingCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        result.State.CloneStatus.Should().Be(CloneStatusNames.Ready);
        fixture.Queue.Enqueued.Should().BeEmpty();
        await fixture.Bindings.DidNotReceive().SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Refresh на чужой intent_id не находит binding (cross-tenant guard)")]
    public async Task Refresh_cross_intent_throws_not_found()
    {
        var fixture = new ServiceFixture();
        var binding = NewBinding(intentId: "other-intent");
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);

        var act = () => fixture.Service.RefreshAsync(
            new RefreshRepositoryBindingCommand(IntentIdValue, binding.Id.Value), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.RepositoryBindingNotFound);
        fixture.Queue.Enqueued.Should().BeEmpty();
    }
}
