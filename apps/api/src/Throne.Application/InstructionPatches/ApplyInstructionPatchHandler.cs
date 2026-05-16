using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

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
    ApplyInstructionPatchWorkflow workflow,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<InstructionPatch> HandleAsync(ApplyInstructionPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patch = await patches.GetAsync(command.PatchId, ct)
            ?? throw InstructionPatchExceptions.NotFound(command.PatchId);
        InstructionPatchOwnerGuard.EnsureOwner(patch, currentUser);
        if (patch.State.Status != InstructionPatchStatusNames.Proposed)
        {
            throw InstructionPatchExceptions.AlreadyDecided(patch);
        }

        var instruction = await UserInstructionLookup.FindAsync(
            instructions, patch.Identity.OwnerUserId, patch.Identity.TargetKind, ct);
        ApplyInstructionPatchValidation.EnsureBaseVersionMatches(patch, instruction);

        var newText = command.FinalText ?? patch.PatchText;
        var now = clock.GetUtcNow();
        return await workflow.ExecuteAsync(patch, instruction, newText, now, ct);
    }
}
