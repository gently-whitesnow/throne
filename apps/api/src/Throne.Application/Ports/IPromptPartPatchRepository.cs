using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="PromptPartPatch"/>.
/// </summary>
public interface IPromptPartPatchRepository
{
    /// <summary>
    /// Insert a fresh <see cref="PromptPartPatch"/>. When <paramref name="idempotencyKey"/>
    /// is supplied the repository deduplicates retries: a second call with the same
    /// idempotency_key returns the originally inserted patch with <c>IsExisting = true</c>
    /// instead of creating a new row.
    /// </summary>
    Task<CreatePromptPartPatchOutcome> CreateAsync(PromptPartPatch patch, string? idempotencyKey, CancellationToken ct);

    /// <summary>
    /// Returns the patch previously created under <paramref name="idempotencyKey"/>,
    /// or <c>null</c> if no such key was ever recorded.
    /// </summary>
    Task<PromptPartPatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    Task<PromptPartPatch?> GetAsync(string id, CancellationToken ct);

    /// <summary>
    /// Pagination order is descending by <c>created_at</c> so the most recent
    /// proposals surface first; cursor is opaque (base64 of (id, created_at)).
    /// </summary>
    Task<PromptPartPatchPage> ListAsync(
        PromptPartPatchListFilter filter,
        int limit,
        string? cursor,
        CancellationToken ct);

    Task<ApplyPromptPartPatchPersistenceOutcome> ApplyAsync(
        PromptPartPatch patch,
        CancellationToken ct);

    Task<RejectPromptPartPatchPersistenceOutcome> RejectAsync(
        PromptPartPatch patch,
        CancellationToken ct);
}

/// <summary>
/// Optional filter set for <see cref="IPromptPartPatchRepository.ListAsync"/>.
/// Null fields mean «do not filter» on that dimension.
/// </summary>
public sealed record PromptPartPatchListFilter(string? TargetScope, string? TargetKey, string? Status);

public sealed record PromptPartPatchPage(
    IReadOnlyList<PromptPartPatch> Items,
    string? NextCursor);
