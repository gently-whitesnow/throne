using Throne.Application.InstructionPatches;
using Throne.Domain.Instructions;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="InstructionPatch"/>. Owner filtering is
/// mandatory on every read path (legacy owner-scoping — see ADR-0029).
/// </summary>
public interface IInstructionPatchRepository
{
    /// <summary>
    /// Insert a fresh <see cref="InstructionPatch"/>. When <paramref name="idempotencyKey"/>
    /// is supplied the repository deduplicates retries from the same owner: a second
    /// call with the same (owner_user_id, idempotency_key) tuple returns the originally
    /// inserted patch with <c>IsExisting = true</c> instead of creating a new row.
    /// Keys are scoped to the owner so cross-user collisions are impossible.
    /// </summary>
    Task<CreateInstructionPatchOutcome> CreateAsync(InstructionPatch patch, string? idempotencyKey, CancellationToken ct);

    /// <summary>
    /// Returns the patch previously created under <paramref name="idempotencyKey"/>
    /// for the current owner, or <c>null</c> if no such key was ever recorded.
    /// </summary>
    Task<InstructionPatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);

    Task<InstructionPatch?> GetAsync(string id, CancellationToken ct);

    /// <summary>
    /// Pagination order is descending by <c>created_at</c> so the most recent
    /// proposals surface first; cursor is opaque (base64 of (id, created_at)).
    /// </summary>
    Task<InstructionPatchPage> ListAsync(
        InstructionPatchListFilter filter,
        int limit,
        string? cursor,
        CancellationToken ct);

    Task<ApplyInstructionPatchPersistenceOutcome> ApplyAsync(
        InstructionPatch patch,
        CancellationToken ct);

    Task<RejectInstructionPatchPersistenceOutcome> RejectAsync(
        InstructionPatch patch,
        CancellationToken ct);
}

/// <summary>
/// Optional filter set for <see cref="IInstructionPatchRepository.ListAsync"/>.
/// Null fields mean «do not filter» on that dimension.
/// </summary>
public sealed record InstructionPatchListFilter(string? TargetKind, string? Status);

public sealed record InstructionPatchPage(
    IReadOnlyList<InstructionPatch> Items,
    string? NextCursor);
