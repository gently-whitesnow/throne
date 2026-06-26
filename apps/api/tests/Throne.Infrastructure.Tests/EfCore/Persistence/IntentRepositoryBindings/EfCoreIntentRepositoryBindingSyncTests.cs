using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.EfCore.Persistence.IntentRepositoryBindings;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfCoreIntentRepositoryBindingSyncTests(SqliteFixture fixture)
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

        (await IndexIsUniqueAsync(scope.Database, "intent_coordinate_unique")).Should().BeTrue();
        (await IndexColumnsAsync(scope.Database, "intent_coordinate_unique"))
            .Should().Equal("intent_id", "provider", "host", "owner", "repo");
    }

    [Fact(DisplayName = "Индексы intent_id и pr_state_last_synced_at созданы")]
    public async Task Secondary_indexes_present()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);

        (await IndexColumnsAsync(scope.Database, "intent_id")).Should().Equal("intent_id");
        (await IndexColumnsAsync(scope.Database, "pr_state_last_synced_at"))
            .Should().Equal("pull_request_state", "last_synced_at");
    }

    private static async Task<bool> IndexIsUniqueAsync(SqliteTestDatabase database, string indexName)
    {
        await using var ctx = await database.CreateContextAsync();
        var connection = ctx.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA index_list('intent_repository_bindings');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1) == indexName)
            {
                return reader.GetInt32(2) == 1;
            }
        }
        return false;
    }

    private static async Task<IReadOnlyList<string>> IndexColumnsAsync(
        SqliteTestDatabase database,
        string indexName)
    {
        await using var ctx = await database.CreateContextAsync();
        var connection = ctx.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info('{indexName}');";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(2));
        }
        return columns;
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
            binding.RecordSync(etag: "W/\"abc\"", lastSeenReviewCommentAt: null, lastSyncedAt.Value);
        }
        await scope.UnitOfWork.ExecuteAsync(
            ct => scope.Repository.CreateAsync(binding, ct),
            CancellationToken.None);
        return binding.Id.Value;
    }
}
