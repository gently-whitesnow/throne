using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoInstructionPatchRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет patch; GetAsync возвращает его в текущем owner-скоупе")]
    public async Task Create_persists_and_owner_scoped()
    {
        var (_, repo, uow) = await NewScopeAsync("user-1");

        var patch = MakePatch("p-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(patch, idempotencyKey: null, ct), CancellationToken.None);

        var loaded = await repo.GetAsync("p-1", CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Identity.OwnerUserId.Should().Be("user-1");
        loaded.State.Status.Should().Be(InstructionPatchStatusNames.Proposed);
        loaded.PatchText.Should().Be(patch.PatchText);
    }

    [Fact(DisplayName = "GetAsync фильтрует по owner и не отдаёт чужие patches")]
    public async Task Get_filters_by_owner()
    {
        var (_, asUser1, uow) = await NewScopeAsync("user-1");
        var (_, asUser2, _) = await NewScopeAsync("user-2", reuse: true);

        await uow.ExecuteAsync(ct => asUser1.CreateAsync(MakePatch("p-1"), idempotencyKey: null, ct), CancellationToken.None);

        (await asUser1.GetAsync("p-1", CancellationToken.None)).Should().NotBeNull();
        (await asUser2.GetAsync("p-1", CancellationToken.None)).Should().BeNull();
    }

    [Fact(DisplayName = "ApplyAsync переводит status и фиксирует applied_text/version; конкурент возвращает AlreadyDecided")]
    public async Task Apply_persists_and_handles_concurrent_decide()
    {
        var (_, repo, uow) = await NewScopeAsync("user-1");

        var patch = MakePatch("p-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(patch, idempotencyKey: null, ct), CancellationToken.None);

        patch.Apply(editedText: null, appliedInstructionVersion: 6, Now);

        var first = await uow.ExecuteAsync(ct => repo.ApplyAsync(patch, ct), CancellationToken.None);
        first.Should().BeOfType<ApplyInstructionPatchPersistenceOutcome.Applied>();

        var stored = await repo.GetAsync("p-1", CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.State.Status.Should().Be(InstructionPatchStatusNames.Applied);
        stored.State.AppliedInstructionVersion.Should().Be(6);
        stored.State.AppliedText.Should().Be(patch.PatchText);

        // Second apply against stale (proposed) snapshot returns AlreadyDecided.
        var stale = MakePatch("p-1");
        stale.Apply(editedText: null, appliedInstructionVersion: 6, Now);
        var second = await uow.ExecuteAsync(ct => repo.ApplyAsync(stale, ct), CancellationToken.None);
        second.Should().BeOfType<ApplyInstructionPatchPersistenceOutcome.AlreadyDecided>();
    }

    [Fact(DisplayName = "RejectAsync переводит в rejected и сохраняет reject_comment")]
    public async Task Reject_persists_comment()
    {
        var (_, repo, uow) = await NewScopeAsync("user-1");

        var patch = MakePatch("p-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(patch, idempotencyKey: null, ct), CancellationToken.None);

        patch.Reject("operator wrote a long enough reason", Now);
        var outcome = await uow.ExecuteAsync(ct => repo.RejectAsync(patch, ct), CancellationToken.None);
        outcome.Should().BeOfType<RejectInstructionPatchPersistenceOutcome.Rejected>();

        var stored = await repo.GetAsync("p-1", CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.State.Status.Should().Be(InstructionPatchStatusNames.Rejected);
        stored.State.RejectComment.Should().Be("operator wrote a long enough reason");
    }

    [Fact(DisplayName = "Idempotency: повторный Create с тем же ключом возвращает существующий patch (IsExisting=true), второй insert отсутствует")]
    public async Task Idempotency_returns_existing_on_retry()
    {
        var (_, repo, uow) = await NewScopeAsync("user-1");
        await EnsureIndexesAsync();

        var first = MakePatch("p-1");
        var firstOutcome = await uow.ExecuteOutsideTransactionAsync(
            ct => repo.CreateAsync(first, idempotencyKey: "dream-key-1", ct),
            CancellationToken.None);
        firstOutcome.IsExisting.Should().BeFalse();
        firstOutcome.Events.Should().HaveCount(1);

        // Second call with same key but a *different* patch payload: sparse-unique
        // index trips, repository resolves the original row instead of inserting.
        var retry = MakePatch("p-2");
        var retryOutcome = await uow.ExecuteOutsideTransactionAsync(
            ct => repo.CreateAsync(retry, idempotencyKey: "dream-key-1", ct),
            CancellationToken.None);
        retryOutcome.IsExisting.Should().BeTrue();
        retryOutcome.Patch.Identity.Id.Should().Be("p-1");
        retryOutcome.Events.Should().BeEmpty();

        var direct = await repo.GetByIdempotencyKeyAsync("dream-key-1", CancellationToken.None);
        direct.Should().NotBeNull();
        direct!.Identity.Id.Should().Be("p-1");
    }

    [Fact(DisplayName = "Idempotency: ключ скоупится по owner — два разных user могут использовать один и тот же ключ")]
    public async Task Idempotency_is_owner_scoped()
    {
        var (_, asUser1, uow) = await NewScopeAsync("user-1");
        var (_, asUser2, _) = await NewScopeAsync("user-2", reuse: true);
        await EnsureIndexesAsync();

        await uow.ExecuteOutsideTransactionAsync(
            ct => asUser1.CreateAsync(MakePatch("u1-p1"), idempotencyKey: "shared-key", ct),
            CancellationToken.None);
        var u2Outcome = await uow.ExecuteOutsideTransactionAsync(
            ct => asUser2.CreateAsync(MakePatchForUser("u2-p1", "user-2"), idempotencyKey: "shared-key", ct),
            CancellationToken.None);

        u2Outcome.IsExisting.Should().BeFalse();
        u2Outcome.Patch.Identity.Id.Should().Be("u2-p1");
    }

    [Fact(DisplayName = "ListAsync owner-scoped + status filter")]
    public async Task List_owner_and_status()
    {
        var (_, repo, uow) = await NewScopeAsync("user-1");

        await uow.ExecuteAsync(ct => repo.CreateAsync(MakePatch("p-1"), idempotencyKey: null, ct), CancellationToken.None);
        await uow.ExecuteAsync(ct => repo.CreateAsync(MakePatch("p-2"), idempotencyKey: null, ct), CancellationToken.None);

        var all = await repo.ListAsync(
            new InstructionPatchListFilter(TargetKind: null, Status: null),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        all.Items.Should().HaveCount(2);

        var rejectedOnly = await repo.ListAsync(
            new InstructionPatchListFilter(TargetKind: null, Status: InstructionPatchStatusNames.Rejected),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        rejectedOnly.Items.Should().BeEmpty();
    }

    private static InstructionPatch MakePatch(string id) => MakePatchForUser(id, "user-1");

    private static InstructionPatch MakePatchForUser(string id, string ownerUserId) =>
        InstructionPatch.Create(
            id: id,
            ownerUserId: ownerUserId,
            targetKind: InstructionKindNames.Work,
            patchText: "patch payload",
            evidenceCardIds: ["card-1"],
            rationale: "rationale",
            baseInstructionVersion: 5,
            now: Now);

    private static async Task EnsureIndexesAsync()
    {
        // Idempotency dedup relies on the sparse-unique index; integration tests
        // don't boot MongoIndexInitializer, so create indexes for the shared DB
        // before exercising the retry race.
        await MongoInstructionPatchIndexes.CreateAsync(_sharedDb!, CancellationToken.None);
    }

    private static IMongoDatabase? _sharedDb;
    private static MongoSessionAccessor? _sharedSession;
    private static MongoUnitOfWork? _sharedUow;

    private async Task<(IMongoDatabase Db, MongoInstructionPatchRepository Repo, MongoUnitOfWork Uow)> NewScopeAsync(
        string userId,
        bool reuse = false)
    {
        if (!reuse || _sharedDb is null)
        {
            var name = $"throne_test_{Guid.NewGuid():N}";
            await fixture.Client.DropDatabaseAsync(name);
            _sharedDb = fixture.Client.GetDatabase(name);
            _sharedSession = new MongoSessionAccessor();
            _sharedUow = new MongoUnitOfWork(fixture.Client, _sharedSession);
        }

        var repo = new MongoInstructionPatchRepository(_sharedDb!, _sharedSession!, new TestCurrentUserAccessor(userId));
        return (_sharedDb!, repo, _sharedUow!);
    }
}
