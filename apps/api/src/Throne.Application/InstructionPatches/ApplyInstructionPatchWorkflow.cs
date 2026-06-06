using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// The transactional core of the apply-patch operation: <see cref="ApplyInstructionPatchHandler"/>
/// runs the pre-flight checks and delegates the unit-of-work choreography here.
/// </summary>
public sealed class ApplyInstructionPatchWorkflow(
    IInstructionPatchRepository patches,
    IInstructionRepository instructions,
    IUnitOfWork unitOfWork)
{
    public Task<InstructionPatch> ExecuteAsync(
        InstructionPatch patch,
        Instruction? instruction,
        string newText,
        DateTimeOffset now,
        CancellationToken ct) =>
        instruction is null
            ? ApplyAsInitialCreateAsync(patch, newText, now, ct)
            : ApplyOverExistingAsync(patch, instruction, newText, now, ct);

    private Task<InstructionPatch> ApplyOverExistingAsync(
        InstructionPatch patch,
        Instruction instruction,
        string newText,
        DateTimeOffset now,
        CancellationToken ct) =>
        unitOfWork.ExecuteAsync<InstructionPatch>(async inner =>
        {
            var replaceOutcome = await instructions.ReplaceTextAsync(
                new InstructionId(instruction.Id.Value),
                patch.Identity.BaseInstructionVersion,
                instruction.Text,
                newText,
                TextVersionAuthor.User,
                now,
                inner);
            var updatedInstruction = InstructionPatchOutcomeMapper.UnwrapReplace(
                replaceOutcome, patch, instruction.Id.Value);

            return await PersistApplyAsync(patch, newText, updatedInstruction.CurrentVersion, now, inner);
        }, ct);

    private Task<InstructionPatch> ApplyAsInitialCreateAsync(
        InstructionPatch patch,
        string newText,
        DateTimeOffset now,
        CancellationToken ct) =>
        unitOfWork.ExecuteAsync<InstructionPatch>(async inner =>
        {
            var instruction = Instruction.Create(
                id: InstructionId.New(),
                scope: InstructionScopeNames.User,
                userId: patch.Identity.OwnerUserId,
                kind: patch.Identity.TargetKind,
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

            return await PersistApplyAsync(patch, newText, instruction.CurrentVersion, now, inner);
        }, ct);

    private async Task<InstructionPatch> PersistApplyAsync(
        InstructionPatch patch,
        string newText,
        int appliedInstructionVersion,
        DateTimeOffset now,
        CancellationToken ct)
    {
        patch.Apply(newText, appliedInstructionVersion, now);
        var outcome = await patches.ApplyAsync(patch, ct);
        return InstructionPatchOutcomeMapper.UnwrapApply(outcome, patch.Identity.Id);
    }
}
