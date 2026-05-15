using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.InstructionPatches;

public sealed record ApplyInstructionPatchCommand(string PatchId, string? FinalText);

/// <summary>
/// User-driven apply path. Modes:
///   * verbatim — <c>final_text</c> omitted or equal to <c>patch_text</c>;
///   * with edit — <c>final_text</c> differs (status becomes
///     <c>applied_edited</c>, <c>applied_text</c> stores the user's text).
///
/// Validates <c>base_instruction_version</c> against the live Instruction;
/// mismatch surfaces as <see cref="ErrorCodes.InstructionPatchNeedsRebase"/>
/// without mutation. On success: replaces the entire Instruction.text
/// (single-shot replace, not a learned-rules injection — the agent supplies the
/// full new instruction body), records the post-replace
/// <c>applied_instruction_version</c> and marks the patch as applied. Evidence
/// card ids carried on the patch are opaque strings (frontier-supplied
/// references) after the ADR-0022 demolition of the server-side InsightCard
/// pipeline; no cross-aggregate cascade is performed here.
/// </summary>
public sealed class ApplyInstructionPatchHandler(
    IInstructionPatchRepository patches,
    IInstructionRepository instructions,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<InstructionPatch> HandleAsync(ApplyInstructionPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patch = await patches.GetAsync(command.PatchId, ct)
            ?? throw NotFound(command.PatchId);
        EnsureOwner(patch);
        if (patch.Status != InstructionPatchStatusNames.Proposed)
        {
            throw AlreadyDecided(patch);
        }

        var instruction = await FindUserInstructionAsync(patch.OwnerUserId, patch.TargetKind, ct);
        if (instruction is null && patch.BaseInstructionVersion != 0)
        {
            throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"User instruction with kind '{patch.TargetKind}' not found.",
                new Dictionary<string, object?> { ["target_kind"] = patch.TargetKind });
        }
        if (instruction is not null && instruction.CurrentVersion != patch.BaseInstructionVersion)
        {
            throw NeedsRebase(patch, instruction.CurrentVersion);
        }

        var newText = command.FinalText ?? patch.PatchText;
        var now = clock.GetUtcNow();

        if (instruction is null)
        {
            return await ApplyAsInitialCreateAsync(patch, newText, now, ct);
        }

        var oldText = instruction.Text;

        var applied = await unitOfWork.ExecuteAsync<InstructionPatch>(async inner =>
        {
            var replaceOutcome = await instructions.ReplaceTextAsync(
                new InstructionId(instruction.Id.Value),
                patch.BaseInstructionVersion,
                oldText,
                newText,
                TextVersionAuthor.User,
                now,
                inner);

            var updatedInstruction = replaceOutcome switch
            {
                ReplaceInstructionTextOutcome.Replaced replaced => replaced.Instruction,
                ReplaceInstructionTextOutcome.VersionConflict vc => throw NeedsRebase(patch, vc.CurrentVersion),
                ReplaceInstructionTextOutcome.NotFound => throw new ApiException(
                    ErrorCodes.InstructionNotFound,
                    $"User instruction with kind '{patch.TargetKind}' not found.",
                    new Dictionary<string, object?> { ["target_kind"] = patch.TargetKind }),
                ReplaceInstructionTextOutcome.MatchNotFound matchNotFound => throw new ApiException(
                    ErrorCodes.InstructionTextMatchNotFound,
                    "old_text was not found in Instruction.text.",
                    new Dictionary<string, object?>
                    {
                        ["instruction_id"] = instruction.Id.Value,
                        ["query_preview"] = matchNotFound.QueryPreview,
                    }),
                ReplaceInstructionTextOutcome.MatchAmbiguous ambiguous => throw new ApiException(
                    ErrorCodes.InstructionTextMatchAmbiguous,
                    "old_text matches more than one location.",
                    new Dictionary<string, object?>
                    {
                        ["instruction_id"] = instruction.Id.Value,
                        ["matches_count"] = ambiguous.MatchesCount,
                    }),
                _ => throw new InvalidOperationException(
                    $"Unhandled instruction replace outcome: {replaceOutcome.GetType().Name}"),
            };

            InstructionPatchTransitions.Apply(patch, newText, updatedInstruction.CurrentVersion, now);
            var persistOutcome = await patches.ApplyAsync(patch, inner);
            var stored = persistOutcome switch
            {
                ApplyInstructionPatchPersistenceOutcome.Applied applied => applied.Patch,
                ApplyInstructionPatchPersistenceOutcome.AlreadyDecided ad => throw AlreadyDecided(ad.Patch),
                ApplyInstructionPatchPersistenceOutcome.NotFound => throw NotFound(patch.Id),
                _ => throw new InvalidOperationException(
                    $"Unhandled patch apply outcome: {persistOutcome.GetType().Name}"),
            };

            return stored;
        }, ct);

        return applied;
    }

    private async Task<InstructionPatch> ApplyAsInitialCreateAsync(
        InstructionPatch patch,
        string newText,
        DateTimeOffset now,
        CancellationToken ct)
    {
        return await unitOfWork.ExecuteAsync<InstructionPatch>(async inner =>
        {
            var instruction = Instruction.Create(
                id: InstructionId.New(),
                scope: InstructionScopeNames.User,
                userId: patch.OwnerUserId,
                kind: patch.TargetKind,
                text: newText,
                now: now);
            var initialVersion = TextVersion.CreateSnapshot(
                id: Guid.NewGuid().ToString("N"),
                ownerKind: TextVersionOwnerKind.Instruction,
                ownerId: instruction.Id.Value,
                snapshot: instruction.Text,
                changedAt: now,
                changedBy: TextVersionAuthor.User);
            await instructions.CreateAsync(instruction, initialVersion, inner);

            InstructionPatchTransitions.Apply(patch, newText, instruction.CurrentVersion, now);
            var persistOutcome = await patches.ApplyAsync(patch, inner);
            return persistOutcome switch
            {
                ApplyInstructionPatchPersistenceOutcome.Applied applied => applied.Patch,
                ApplyInstructionPatchPersistenceOutcome.AlreadyDecided ad => throw AlreadyDecided(ad.Patch),
                ApplyInstructionPatchPersistenceOutcome.NotFound => throw NotFound(patch.Id),
                _ => throw new InvalidOperationException(
                    $"Unhandled patch apply outcome: {persistOutcome.GetType().Name}"),
            };
        }, ct);
    }

    private async Task<Instruction?> FindUserInstructionAsync(string ownerUserId, string kind, CancellationToken ct)
    {
        var list = await instructions.GetUserInstructionsByKindsAsync(ownerUserId, [kind], ct);
        return list.Count == 0 ? null : list[0];
    }

    private void EnsureOwner(InstructionPatch patch)
    {
        if (!string.Equals(patch.OwnerUserId, currentUser.UserId, StringComparison.Ordinal))
        {
            throw NotFound(patch.Id);
        }
    }

    private static ApiException NotFound(string patchId) => new(
        ErrorCodes.InstructionPatchNotFound,
        $"InstructionPatch '{patchId}' not found.",
        new Dictionary<string, object?> { ["patch_id"] = patchId });

    private static ApiException AlreadyDecided(InstructionPatch patch) => new(
        ErrorCodes.InstructionPatchAlreadyDecided,
        $"InstructionPatch '{patch.Id}' is in status '{patch.Status}'.",
        new Dictionary<string, object?>
        {
            ["patch_id"] = patch.Id,
            ["current_status"] = patch.Status,
        });

    private static ApiException NeedsRebase(InstructionPatch patch, int currentVersion) => new(
        ErrorCodes.InstructionPatchNeedsRebase,
        "Instruction.current_version moved past patch.base_instruction_version.",
        new Dictionary<string, object?>
        {
            ["patch_id"] = patch.Id,
            ["target_kind"] = patch.TargetKind,
            ["base_instruction_version"] = patch.BaseInstructionVersion,
            ["current_instruction_version"] = currentVersion,
        });
}
