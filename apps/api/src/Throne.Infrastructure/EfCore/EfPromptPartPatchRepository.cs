using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;
using Throne.Infrastructure.EfCore.PromptParts;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Composite EF Core repository that fronts <see cref="IPromptPartPatchRepository"/>.
/// All actual work is delegated to per-concern services in <c>PromptParts/</c> so each
/// file stays well under the per-type budget.
/// </summary>
internal sealed class EfPromptPartPatchRepository(
    EfPromptPartPatchLifecycle lifecycle,
    EfPromptPartPatchDecisionMutator decision)
    : IPromptPartPatchRepository
{
    public Task<CreatePromptPartPatchOutcome> CreateAsync(
        PromptPartPatch patch,
        string? idempotencyKey,
        CancellationToken ct) =>
        lifecycle.CreateAsync(patch, idempotencyKey, ct);

    public Task<PromptPartPatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        lifecycle.GetByIdempotencyKeyAsync(idempotencyKey, ct);

    public Task<PromptPartPatch?> GetAsync(string id, CancellationToken ct) =>
        lifecycle.GetAsync(id, ct);

    public Task<PromptPartPatchPage> ListAsync(
        PromptPartPatchListFilter filter,
        int limit,
        string? cursor,
        CancellationToken ct) =>
        lifecycle.ListAsync(filter, limit, cursor, ct);

    public Task<ApplyPromptPartPatchPersistenceOutcome> ApplyAsync(
        PromptPartPatch patch,
        CancellationToken ct) =>
        decision.ApplyAsync(patch, ct);

    public Task<RejectPromptPartPatchPersistenceOutcome> RejectAsync(
        PromptPartPatch patch,
        CancellationToken ct) =>
        decision.RejectAsync(patch, ct);
}
