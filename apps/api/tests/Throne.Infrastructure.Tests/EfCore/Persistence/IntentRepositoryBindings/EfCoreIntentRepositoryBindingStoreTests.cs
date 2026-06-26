using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.EfCore.Persistence.IntentRepositoryBindings;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfCoreIntentRepositoryBindingStoreTests(SqliteFixture fixture)
{
    [Fact(DisplayName = "CreateAsync пишет binding в коллекцию intent_repository_bindings")]
    public async Task Create_persists_document()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var binding = IntentRepositoryBindingTestFactory.NewBinding(intentId, prNumber: 17);

        var outcome = await CreateAsync(scope, binding);

        outcome.Should().BeOfType<CreateBindingOutcome.Created>()
            .Subject.Binding.Id.Should().Be(binding.Id);

        var stored = await FindBindingRowAsync(scope.Database, binding.Id.Value);
        stored.Should().NotBeNull();
        stored!.IntentId.Should().Be(intentId.Value);
        stored.Provider.Should().Be(GitProviderNames.GitHub);
        stored.Owner.Should().Be("octo");
        stored.Repo.Should().Be("throne");
        stored.CloneStatus.Should().Be(CloneStatusNames.Pending);
        stored.PullRequestNumber.Should().Be(17);
        stored.PullRequestState.Should().BeNull();
    }

    [Fact(DisplayName = "CreateAsync для повторной пары (intent, provider, owner, repo) возвращает Duplicate")]
    public async Task Create_duplicate_returns_existing()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var first = IntentRepositoryBindingTestFactory.NewBinding(intentId);
        var second = IntentRepositoryBindingTestFactory.NewBinding(intentId); // same coordinate, fresh id

        await CreateAsync(scope, first);
        var outcome = await CreateAsync(scope, second);

        var duplicate = outcome.Should().BeOfType<CreateBindingOutcome.Duplicate>().Subject;
        duplicate.Existing.Id.Should().Be(first.Id);
    }

    [Fact(DisplayName = "GetByIdAsync возвращает доменный binding по id")]
    public async Task Get_returns_domain_binding()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var binding = IntentRepositoryBindingTestFactory.NewBinding(intentId, prNumber: 42);
        await CreateAsync(scope, binding);

        var fetched = await scope.Repository.GetByIdAsync(binding.Id, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.IntentId.Should().Be(intentId);
        fetched.State.CloneStatus.Should().Be(CloneStatusNames.Pending);
        fetched.State.PullRequestNumber.Should().Be(42);
    }

    [Fact(DisplayName = "GetByIdAsync возвращает null для несуществующего id")]
    public async Task Get_returns_null_when_missing()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);

        var fetched = await scope.Repository.GetByIdAsync(BindingId.New(), CancellationToken.None);

        fetched.Should().BeNull();
    }

    [Fact(DisplayName = "FindByIntentAsync возвращает bindings, отсортированные по created_at ASC")]
    public async Task FindByIntent_returns_sorted_list()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var older = IntentRepositoryBindingTestFactory.NewBinding(intentId, repo: "alpha", at: IntentRepositoryBindingTestFactory.Now);
        var newer = IntentRepositoryBindingTestFactory.NewBinding(intentId, repo: "beta", at: IntentRepositoryBindingTestFactory.Now.AddMinutes(5));
        await CreateAsync(scope, newer);
        await CreateAsync(scope, older);

        var bindings = await scope.Repository.FindByIntentAsync(intentId, CancellationToken.None);

        bindings.Should().HaveCount(2);
        bindings[0].Id.Should().Be(older.Id);
        bindings[1].Id.Should().Be(newer.Id);
    }

    [Fact(DisplayName = "FindByIntentAsync игнорирует bindings других intent'ов")]
    public async Task FindByIntent_isolates_intents()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var mine = IntentId.New();
        var other = IntentId.New();
        await CreateAsync(scope, IntentRepositoryBindingTestFactory.NewBinding(mine, repo: "mine"));
        await CreateAsync(scope, IntentRepositoryBindingTestFactory.NewBinding(other, repo: "theirs"));

        var bindings = await scope.Repository.FindByIntentAsync(mine, CancellationToken.None);

        bindings.Should().ContainSingle(b => b.Coordinate.Repo == "mine");
    }

    [Fact(DisplayName = "SaveAsync обновляет mutable state binding'а")]
    public async Task Save_updates_state()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var intentId = IntentId.New();
        var binding = IntentRepositoryBindingTestFactory.NewBinding(intentId);
        await CreateAsync(scope, binding);

        binding.MarkCloning(IntentRepositoryBindingTestFactory.Now.AddSeconds(1));
        binding.MarkReady(IntentRepositoryBindingTestFactory.Now.AddSeconds(2));
        var outcome = await SaveAsync(scope, binding);

        outcome.Should().BeOfType<SaveBindingOutcome.Saved>();
        var stored = await scope.Repository.GetByIdAsync(binding.Id, CancellationToken.None);
        stored!.State.CloneStatus.Should().Be(CloneStatusNames.Ready);
    }

    [Fact(DisplayName = "SaveAsync возвращает NotFound, если binding не существует")]
    public async Task Save_missing_returns_not_found()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var binding = IntentRepositoryBindingTestFactory.NewBinding(IntentId.New());

        var outcome = await SaveAsync(scope, binding);

        outcome.Should().BeOfType<SaveBindingOutcome.NotFound>();
    }

    [Fact(DisplayName = "DeleteAsync удаляет binding и возвращает удалённую запись")]
    public async Task Delete_removes_binding()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);
        var binding = IntentRepositoryBindingTestFactory.NewBinding(IntentId.New());
        await CreateAsync(scope, binding);

        var outcome = await DeleteAsync(scope, binding.Id);

        outcome.Should().BeOfType<DeleteBindingOutcome.Deleted>()
            .Subject.Binding.Id.Should().Be(binding.Id);
        (await scope.Repository.GetByIdAsync(binding.Id, CancellationToken.None)).Should().BeNull();
    }

    [Fact(DisplayName = "DeleteAsync для отсутствующего id возвращает NotFound")]
    public async Task Delete_missing_returns_not_found()
    {
        var scope = await IntentRepositoryBindingTestScope.CreateAsync(fixture);

        var outcome = await DeleteAsync(scope, BindingId.New());

        outcome.Should().BeOfType<DeleteBindingOutcome.NotFound>();
    }

    private static async Task<IntentRepositoryBindingRow?> FindBindingRowAsync(
        SqliteTestDatabase database,
        string id)
    {
        await using var ctx = await database.CreateContextAsync();
        return await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    private static Task<CreateBindingOutcome> CreateAsync(
        IntentRepositoryBindingTestScope scope,
        IntentRepositoryBinding binding) =>
        scope.UnitOfWork.ExecuteAsync(
            ct => scope.Repository.CreateAsync(binding, ct),
            CancellationToken.None);

    private static Task<SaveBindingOutcome> SaveAsync(
        IntentRepositoryBindingTestScope scope,
        IntentRepositoryBinding binding) =>
        scope.UnitOfWork.ExecuteAsync(
            ct => scope.Repository.SaveAsync(binding, ct),
            CancellationToken.None);

    private static Task<DeleteBindingOutcome> DeleteAsync(
        IntentRepositoryBindingTestScope scope,
        BindingId id) =>
        scope.UnitOfWork.ExecuteAsync(
            ct => scope.Repository.DeleteAsync(id, ct),
            CancellationToken.None);
}
