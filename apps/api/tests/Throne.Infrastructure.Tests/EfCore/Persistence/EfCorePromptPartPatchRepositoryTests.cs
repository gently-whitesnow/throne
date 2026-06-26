using FluentAssertions;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Infrastructure.Tests.EfCore.Persistence;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfCorePromptPartPatchRepositoryTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет patch; GetAsync возвращает его")]
    public async Task Create_persists_and_reads_back()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var patch = MakePatch("p-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(patch, idempotencyKey: null, ct), CancellationToken.None);

        var loaded = await repo.GetAsync("p-1", CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.State.Status.Should().Be(PromptPartPatchStatusNames.Proposed);
        loaded.PatchText.Should().Be(patch.PatchText);
    }

    [Fact(DisplayName = "ApplyAsync переводит status и фиксирует applied_text/version; конкурент возвращает AlreadyDecided")]
    public async Task Apply_persists_and_handles_concurrent_decide()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var patch = MakePatch("p-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(patch, idempotencyKey: null, ct), CancellationToken.None);

        patch.Apply(editedText: null, appliedVersion: 6, Now);

        var first = await uow.ExecuteAsync(ct => repo.ApplyAsync(patch, ct), CancellationToken.None);
        first.Should().BeOfType<ApplyPromptPartPatchPersistenceOutcome.Applied>();

        var stored = await repo.GetAsync("p-1", CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.State.Status.Should().Be(PromptPartPatchStatusNames.Applied);
        stored.State.AppliedVersion.Should().Be(6);
        stored.State.AppliedText.Should().Be(patch.PatchText);

        // Second apply against stale (proposed) snapshot returns AlreadyDecided.
        var stale = MakePatch("p-1");
        stale.Apply(editedText: null, appliedVersion: 6, Now);
        var second = await uow.ExecuteAsync(ct => repo.ApplyAsync(stale, ct), CancellationToken.None);
        second.Should().BeOfType<ApplyPromptPartPatchPersistenceOutcome.AlreadyDecided>();
    }

    [Fact(DisplayName = "RejectAsync переводит в rejected и сохраняет reject_comment")]
    public async Task Reject_persists_comment()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var patch = MakePatch("p-1");
        await uow.ExecuteAsync(ct => repo.CreateAsync(patch, idempotencyKey: null, ct), CancellationToken.None);

        patch.Reject("operator wrote a long enough reason", Now);
        var outcome = await uow.ExecuteAsync(ct => repo.RejectAsync(patch, ct), CancellationToken.None);
        outcome.Should().BeOfType<RejectPromptPartPatchPersistenceOutcome.Rejected>();

        var stored = await repo.GetAsync("p-1", CancellationToken.None);
        stored.Should().NotBeNull();
        stored!.State.Status.Should().Be(PromptPartPatchStatusNames.Rejected);
        stored.State.RejectComment.Should().Be("operator wrote a long enough reason");
    }

    [Fact(DisplayName = "Idempotency: повторный Create с тем же ключом возвращает существующий patch (IsExisting=true), второй insert отсутствует")]
    public async Task Idempotency_returns_existing_on_retry()
    {
        var (_, repo, uow) = await NewScopeAsync();

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

    [Fact(DisplayName = "ListAsync status filter")]
    public async Task List_status_filter()
    {
        var (_, repo, uow) = await NewScopeAsync();

        await uow.ExecuteAsync(ct => repo.CreateAsync(MakePatch("p-1"), idempotencyKey: null, ct), CancellationToken.None);
        await uow.ExecuteAsync(ct => repo.CreateAsync(MakePatch("p-2"), idempotencyKey: null, ct), CancellationToken.None);

        var all = await repo.ListAsync(
            new PromptPartPatchListFilter(TargetScope: null, TargetKey: null, Status: null),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        all.Items.Should().HaveCount(2);

        var rejectedOnly = await repo.ListAsync(
            new PromptPartPatchListFilter(TargetScope: null, TargetKey: null, Status: PromptPartPatchStatusNames.Rejected),
            limit: 50,
            cursor: null,
            CancellationToken.None);
        rejectedOnly.Items.Should().BeEmpty();
    }

    private static PromptPartPatch MakePatch(string id) =>
        PromptPartPatch.Create(
            id: id,
            targetScope: PromptPartScopeNames.User,
            targetKey: "work",
            patchText: "patch payload",
            rationale: "rationale",
            baseVersion: 5,
            now: Now);

    private async Task<(SqliteTestDatabase Db, IPromptPartPatchRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db, db.GetRequiredService<IPromptPartPatchRepository>(), db.GetRequiredService<IUnitOfWork>());
    }
}
