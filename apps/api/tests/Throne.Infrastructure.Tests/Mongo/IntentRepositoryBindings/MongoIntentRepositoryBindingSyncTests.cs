using FluentAssertions;
using MongoDB.Driver;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo.IntentRepositoryBindings;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentRepositoryBindingSyncTests(MongoFixture fixture)
{
    [Fact(DisplayName = "FindOpenForSyncAsync возвращает ready + PR + state=open/null, отсортированные по last_synced_at ASC")]
    public async Task FindOpenForSync_filters_and_orders()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();

        // Eligible: ready + PR + state=open, never polled.
        var openNew = await PersistReadyOpenAsync(scope, intentId, "alpha", lastSyncedAt: null);
        // Eligible: polled an hour ago — should come AFTER the never-polled one.
        var openOlder = await PersistReadyOpenAsync(
            scope,
            intentId,
            "beta",
            lastSyncedAt: IntentRepositoryBindingTestFactory.Now.AddMinutes(-60));
        // Eligible: initial bind with PR attached but state not observed yet.
        var initial = await PersistAsync(
            scope,
            intentId,
            "theta",
            clone: CloneStatusNames.Ready,
            prNumber: 1,
            prState: null,
            lastSyncedAt: IntentRepositoryBindingTestFactory.Now.AddMinutes(-30));
        // Eligible: polled recently — should come last.
        var openRecent = await PersistReadyOpenAsync(
            scope,
            intentId,
            "gamma",
            lastSyncedAt: IntentRepositoryBindingTestFactory.Now.AddMinutes(-5));

        // Excluded: closed PR.
        await PersistAsync(scope, intentId, "delta", clone: CloneStatusNames.Ready, prNumber: 1, prState: PullRequestStateNames.Closed);
        // Excluded: still pending.
        await PersistAsync(scope, intentId, "epsilon", clone: CloneStatusNames.Pending, prNumber: 1, prState: PullRequestStateNames.Open);
        // Excluded: ready but no PR attached.
        await PersistAsync(scope, intentId, "zeta", clone: CloneStatusNames.Ready, prNumber: null, prState: null);
        // Excluded: broken upstream — PR-sync должен пропустить.
        await PersistAsync(scope, intentId, "eta", clone: CloneStatusNames.Broken, prNumber: 1, prState: PullRequestStateNames.Open);

        var due = await scope.Repository.FindOpenForSyncAsync(CancellationToken.None);

        due.Select(b => b.Id.Value)
            .Should()
            .Equal(openNew, openOlder, initial, openRecent);
    }

    [Fact(DisplayName = "Уникальный индекс intent_coordinate_unique создан с правильным набором полей")]
    public async Task Unique_index_present()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var indexes = await scope.Database
            .GetCollection<IntentRepositoryBindingDocument>(MongoCollectionNames.IntentRepositoryBindings)
            .Indexes.List()
            .ToListAsync();

        var unique = indexes.Single(i => i["name"].AsString == "intent_coordinate_unique");
        unique["unique"].AsBoolean.Should().BeTrue();
        var keys = unique["key"].AsBsonDocument;
        keys.ElementCount.Should().Be(4);
        keys.GetElement(0).Name.Should().Be("intent_id");
        keys.GetElement(1).Name.Should().Be("provider");
        keys.GetElement(2).Name.Should().Be("owner");
        keys.GetElement(3).Name.Should().Be("repo");
    }

    [Fact(DisplayName = "Индексы intent_id и pr_state_last_synced_at созданы")]
    public async Task Secondary_indexes_present()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var indexes = await scope.Database
            .GetCollection<IntentRepositoryBindingDocument>(MongoCollectionNames.IntentRepositoryBindings)
            .Indexes.List()
            .ToListAsync();

        indexes.Should().Contain(i => i["name"].AsString == "intent_id");
        var sync = indexes.Single(i => i["name"].AsString == "pr_state_last_synced_at");
        var keys = sync["key"].AsBsonDocument;
        keys.ElementCount.Should().Be(2);
        keys.GetElement(0).Name.Should().Be("pull_request_state");
        keys.GetElement(1).Name.Should().Be("last_synced_at");
    }

    private static async Task<string> PersistReadyOpenAsync(
        IntentRepositoryBindingTestScope scope,
        IntentId intentId,
        string repo,
        DateTimeOffset? lastSyncedAt)
    {
        return await PersistAsync(
            scope,
            intentId,
            repo,
            clone: CloneStatusNames.Ready,
            prNumber: 99,
            prState: PullRequestStateNames.Open,
            lastSyncedAt: lastSyncedAt);
    }

    private static async Task<string> PersistAsync(
        IntentRepositoryBindingTestScope scope,
        IntentId intentId,
        string repo,
        string clone,
        int? prNumber,
        string? prState,
        DateTimeOffset? lastSyncedAt = null)
    {
        var binding = IntentRepositoryBindingTestFactory.NewBinding(intentId, repo: repo, prNumber: prNumber);
        // Drive the status machine forward so we end up in the requested terminal state.
        // pending → cloning is needed before either ready/failed; ready is the prerequisite
        // for broken.
        if (clone is CloneStatusNames.Cloning or CloneStatusNames.Ready or CloneStatusNames.Failed or CloneStatusNames.Broken)
        {
            binding.MarkCloning(IntentRepositoryBindingTestFactory.Now);
        }
        if (clone is CloneStatusNames.Ready or CloneStatusNames.Broken)
        {
            binding.MarkReady(IntentRepositoryBindingTestFactory.Now);
        }
        if (clone == CloneStatusNames.Broken)
        {
            binding.MarkBroken("upstream 404", IntentRepositoryBindingTestFactory.Now);
        }
        if (clone == CloneStatusNames.Failed)
        {
            binding.MarkFailed("clone error", IntentRepositoryBindingTestFactory.Now);
        }
        if (prState is not null && prNumber is not null)
        {
            binding.RecordPullRequestState(prState, IntentRepositoryBindingTestFactory.Now);
        }
        if (lastSyncedAt is not null)
        {
            binding.RecordSync(etag: "W/\"abc\"", lastSyncedAt.Value);
        }
        await scope.Repository.CreateAsync(binding, CancellationToken.None);
        return binding.Id.Value;
    }
}
