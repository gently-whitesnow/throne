using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

public sealed record RejectInstructionPatchCommand(string PatchId, string Comment);

/// <summary>
/// User-driven reject path with a mandatory comment (≥10 chars after trimming).
/// The comment is part of the patch's persistent state — the next analysis
/// round must take it into account so the same proposal is not re-emitted.
/// </summary>
public sealed class RejectInstructionPatchHandler(
    IInstructionPatchRepository patches,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<InstructionPatch> HandleAsync(RejectInstructionPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patch = await patches.GetAsync(command.PatchId, ct)
            ?? throw InstructionPatchExceptions.NotFound(command.PatchId);
        InstructionPatchOwnerGuard.EnsureOwner(patch, currentUser);

        var transition = patch.Reject(command.Comment, clock.GetUtcNow());
        InstructionPatchOutcomeMapper.ThrowForRejectTransition(transition, patch);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => patches.RejectAsync(patch, inner),
            ct);

        return InstructionPatchOutcomeMapper.UnwrapReject(outcome, command.PatchId);
    }
}
