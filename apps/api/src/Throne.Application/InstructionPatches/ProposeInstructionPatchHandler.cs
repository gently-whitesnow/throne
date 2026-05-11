using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

public sealed record ProposeInstructionPatchCommand(
    string TargetKind,
    string PatchText,
    IReadOnlyList<string> EvidenceCardIds,
    string Rationale,
    int BaseInstructionVersion,
    string? IdempotencyKey = null);

internal static class IdempotencyKeyValidator
{
    public const int MaxLength = 64;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"idempotency_key must be ≤{MaxLength} characters.",
                new Dictionary<string, object?> { ["field"] = "idempotency_key" });
        }
        return trimmed;
    }
}

/// <summary>
/// Frontier-agent surface for creating a fresh <see cref="InstructionPatch"/> in
/// status <c>proposed</c>. Validates target_kind and the base_instruction_version
/// (must match the current Instruction version at create time; the apply path
/// re-checks before persisting the new instruction text version).
/// </summary>
public sealed class ProposeInstructionPatchHandler(
    IInstructionPatchRepository patches,
    IInstructionRepository instructions,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<InstructionPatch> HandleAsync(ProposeInstructionPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!InstructionKindNames.IsKnown(command.TargetKind))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Unknown target_kind: {command.TargetKind}.",
                new Dictionary<string, object?> { ["field"] = "target_kind" });
        }
        if (string.IsNullOrWhiteSpace(command.PatchText))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "patch_text must not be empty.",
                new Dictionary<string, object?> { ["field"] = "patch_text" });
        }

        var idempotencyKey = IdempotencyKeyValidator.Normalize(command.IdempotencyKey);
        var ownerUserId = currentUser.UserId;

        // Idempotency check runs BEFORE version validation: a retry that arrives
        // after the original patch was applied (and target Instruction.current_version
        // already bumped) must still resolve to the original patch instead of a
        // misleading 409 needs_rebase.
        if (idempotencyKey is not null)
        {
            var existing = await patches.GetByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null)
            {
                return existing;
            }
        }

        // Resolve target instruction; verify version matches what the agent saw.
        var targetInstruction = await FindUserInstructionAsync(ownerUserId, command.TargetKind, ct)
            ?? throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"User instruction with kind '{command.TargetKind}' not found for the current user.",
                new Dictionary<string, object?> { ["target_kind"] = command.TargetKind });
        if (targetInstruction.CurrentVersion != command.BaseInstructionVersion)
        {
            throw new ApiException(
                ErrorCodes.InstructionPatchNeedsRebase,
                "base_instruction_version does not match Instruction.current_version.",
                new Dictionary<string, object?>
                {
                    ["target_kind"] = command.TargetKind,
                    ["base_instruction_version"] = command.BaseInstructionVersion,
                    ["current_instruction_version"] = targetInstruction.CurrentVersion,
                });
        }

        InstructionPatch patch;
        try
        {
            patch = InstructionPatch.Create(
                id: Guid.NewGuid().ToString("N"),
                ownerUserId: ownerUserId,
                targetKind: command.TargetKind,
                patchText: command.PatchText,
                evidenceCardIds: command.EvidenceCardIds ?? [],
                rationale: command.Rationale ?? string.Empty,
                baseInstructionVersion: command.BaseInstructionVersion,
                now: clock.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                ex.Message,
                new Dictionary<string, object?> { ["field"] = ex.ParamName ?? "patch" });
        }

        // Insert runs outside the UoW transaction: it is a single-doc write
        // (atomic on its own) and the idempotency-retry fallback path needs to
        // SELECT after a DuplicateKey, which a Mongo transaction would forbid
        // (the failed write aborts the txn). For non-idempotent calls the path
        // is functionally identical.
        var outcome = await unitOfWork.ExecuteOutsideTransactionAsync(
            inner => patches.CreateAsync(patch, idempotencyKey, inner),
            ct);
        return outcome.Patch;
    }

    private async Task<Instruction?> FindUserInstructionAsync(string ownerUserId, string kind, CancellationToken ct)
    {
        var list = await instructions.GetUserInstructionsByKindsAsync(ownerUserId, [kind], ct);
        return list.Count == 0 ? null : list[0];
    }
}
