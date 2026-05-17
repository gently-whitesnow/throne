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
            throw InstructionPatchExceptions.ValidationFailed(
                "idempotency_key",
                $"idempotency_key must be ≤{MaxLength} characters.");
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
    UserInstructionLookup userInstructions,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<InstructionPatch> HandleAsync(ProposeInstructionPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ProposeInstructionPatchValidator.ValidateCommand(command);

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
        // Если записи нет — current_version=0 валидно, такой патч позже создаст
        // Instruction на apply-стороне.
        var targetInstruction = await userInstructions.FindAsync(
            ownerUserId, command.TargetKind, ct);
        var currentVersion = targetInstruction?.CurrentVersion ?? 0;
        if (currentVersion != command.BaseInstructionVersion)
        {
            throw new ApiException(
                ErrorCodes.InstructionPatchNeedsRebase,
                "base_instruction_version does not match Instruction.current_version.",
                new Dictionary<string, object?>
                {
                    ["target_kind"] = command.TargetKind,
                    ["base_instruction_version"] = command.BaseInstructionVersion,
                    ["current_instruction_version"] = currentVersion,
                });
        }

        var patch = ProposeInstructionPatchFactory.Create(command, ownerUserId, clock.GetUtcNow());

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
}

internal static class ProposeInstructionPatchValidator
{
    public static void ValidateCommand(ProposeInstructionPatchCommand command)
    {
        if (!InstructionKindNames.IsKnown(command.TargetKind))
        {
            throw InstructionPatchExceptions.UnknownTargetKind(command.TargetKind);
        }
        if (string.IsNullOrWhiteSpace(command.PatchText))
        {
            throw InstructionPatchExceptions.ValidationFailed("patch_text", "patch_text must not be empty.");
        }
    }
}

internal static class ProposeInstructionPatchFactory
{
    public static InstructionPatch Create(
        ProposeInstructionPatchCommand command,
        string ownerUserId,
        DateTimeOffset now)
    {
        try
        {
            return InstructionPatch.Create(
                id: Guid.NewGuid().ToString("N"),
                ownerUserId: ownerUserId,
                targetKind: command.TargetKind,
                patchText: command.PatchText,
                evidenceCardIds: command.EvidenceCardIds ?? [],
                rationale: command.Rationale ?? string.Empty,
                baseInstructionVersion: command.BaseInstructionVersion,
                now: now);
        }
        catch (ArgumentException ex)
        {
            throw InstructionPatchExceptions.ValidationFailed(
                ex.ParamName ?? "patch",
                ex.Message);
        }
    }
}
