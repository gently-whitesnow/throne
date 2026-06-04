using FluentAssertions;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.Mongo.IntentRepositoryBindings;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentRepositoryBindingCloneStatusTests(MongoFixture fixture)
{
    [Fact(DisplayName = "FindByCloneStatusAsync(pending) возвращает только pending, по created_at ASC")]
    public async Task FindByCloneStatus_pending_returns_only_pending_ordered()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();

        var pendingA = await PersistAsync(scope, intentId, "alpha", CloneStatusNames.Pending);
        var pendingB = await PersistAsync(scope, intentId, "beta", CloneStatusNames.Pending);
        await PersistAsync(scope, intentId, "gamma", CloneStatusNames.Cloning);
        await PersistAsync(scope, intentId, "delta", CloneStatusNames.Ready);
        await PersistAsync(scope, intentId, "epsilon", CloneStatusNames.Failed);

        var pending = await scope.Repository.FindByCloneStatusAsync(CloneStatusNames.Pending, CancellationToken.None);

        pending.Select(b => b.Id.Value).Should().Equal(pendingA, pendingB);
        pending.Should().OnlyContain(b => b.State.CloneStatus == CloneStatusNames.Pending);
    }

    [Fact(DisplayName = "FindByCloneStatusAsync(cloning) — пусто, если нет cloning-binding'ов")]
    public async Task FindByCloneStatus_cloning_empty_when_none()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        await PersistAsync(scope, intentId, "alpha", CloneStatusNames.Pending);
        await PersistAsync(scope, intentId, "beta", CloneStatusNames.Ready);

        var cloning = await scope.Repository.FindByCloneStatusAsync(CloneStatusNames.Cloning, CancellationToken.None);

        cloning.Should().BeEmpty();
    }

    [Fact(DisplayName = "FindByCloneStatusAsync(cloning) возвращает binding'и в cloning")]
    public async Task FindByCloneStatus_cloning_returns_cloning()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var stuck = await PersistAsync(scope, intentId, "alpha", CloneStatusNames.Cloning);
        await PersistAsync(scope, intentId, "beta", CloneStatusNames.Pending);

        var cloning = await scope.Repository.FindByCloneStatusAsync(CloneStatusNames.Cloning, CancellationToken.None);

        cloning.Select(b => b.Id.Value).Should().Equal(stuck);
    }

    [Fact(DisplayName = "ClaimCloningAsync: CAS — первый воркер забирает pending→cloning, второй (DB уже cloning) получает NotFound")]
    public async Task ClaimCloning_is_atomic_pending_to_cloning()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var binding = IntentRepositoryBindingTestFactory.NewBinding(IntentId.New());
        await scope.Repository.CreateAsync(binding, CancellationToken.None);

        // Same binding dequeued twice (auto-bind + Run-preflight). Both workers carry the
        // cloning state in-memory; the DB precondition `clone_status == pending` decides the winner.
        binding.MarkCloning(IntentRepositoryBindingTestFactory.Now);
        var first = await scope.Repository.ClaimCloningAsync(binding, CancellationToken.None);
        var second = await scope.Repository.ClaimCloningAsync(binding, CancellationToken.None);

        first.Should().BeOfType<SaveBindingOutcome.Saved>();
        second.Should().BeOfType<SaveBindingOutcome.NotFound>();
        var stored = await scope.Repository.GetByIdAsync(binding.Id, CancellationToken.None);
        stored!.State.CloneStatus.Should().Be(CloneStatusNames.Cloning);
    }

    private static async Task<string> PersistAsync(
        IntentRepositoryBindingTestScope scope,
        IntentId intentId,
        string repo,
        string clone)
    {
        var binding = IntentRepositoryBindingTestFactory.NewBinding(intentId, repo: repo);
        if (clone is CloneStatusNames.Cloning or CloneStatusNames.Ready or CloneStatusNames.Failed)
        {
            binding.MarkCloning(IntentRepositoryBindingTestFactory.Now);
        }
        if (clone == CloneStatusNames.Ready)
        {
            binding.MarkReady(IntentRepositoryBindingTestFactory.Now);
        }
        if (clone == CloneStatusNames.Failed)
        {
            binding.MarkFailed("clone error", IntentRepositoryBindingTestFactory.Now);
        }
        await scope.Repository.CreateAsync(binding, CancellationToken.None);
        return binding.Id.Value;
    }
}
