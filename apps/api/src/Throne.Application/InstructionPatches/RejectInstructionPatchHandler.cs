using Throne.Application.Auth;
using Throne.Application.Errors;
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
            ?? throw NotFound(command.PatchId);
        EnsureOwner(patch);

        var transition = InstructionPatchTransitions.Reject(patch, command.Comment, clock.GetUtcNow());
        switch (transition)
        {
            case InstructionPatchTransitions.RejectResult.AlreadyDecided:
                throw AlreadyDecided(patch);
            case InstructionPatchTransitions.RejectResult.CommentTooShort:
                throw new ApiException(
                    ErrorCodes.ValidationFailed,
                    $"reject_comment must be at least {InstructionPatch.MinRejectCommentLength} characters after trimming.",
                    new Dictionary<string, object?>
                    {
                        ["field"] = "reject_comment",
                        ["min_length"] = InstructionPatch.MinRejectCommentLength,
                    });
            case InstructionPatchTransitions.RejectResult.Ok:
                break;
            default:
                throw new InvalidOperationException($"Unhandled reject result: {transition}");
        }

        var outcome = await unitOfWork.ExecuteAsync(
            inner => patches.RejectAsync(patch, inner),
            ct);

        return outcome switch
        {
            RejectInstructionPatchPersistenceOutcome.Rejected rejected => rejected.Patch,
            RejectInstructionPatchPersistenceOutcome.AlreadyDecided ad => throw AlreadyDecided(ad.Patch),
            RejectInstructionPatchPersistenceOutcome.NotFound => throw NotFound(command.PatchId),
            _ => throw new InvalidOperationException($"Unhandled reject outcome: {outcome.GetType().Name}"),
        };
    }

    private void EnsureOwner(InstructionPatch patch)
    {
        if (!string.Equals(patch.Identity.OwnerUserId, currentUser.UserId, StringComparison.Ordinal))
        {
            throw NotFound(patch.Identity.Id);
        }
    }

    private static ApiException NotFound(string patchId) => new(
        ErrorCodes.InstructionPatchNotFound,
        $"InstructionPatch '{patchId}' not found.",
        new Dictionary<string, object?> { ["patch_id"] = patchId });

    private static ApiException AlreadyDecided(InstructionPatch patch) => new(
        ErrorCodes.InstructionPatchAlreadyDecided,
        $"InstructionPatch '{patch.Identity.Id}' is in status '{patch.State.Status}'.",
        new Dictionary<string, object?>
        {
            ["patch_id"] = patch.Identity.Id,
            ["current_status"] = patch.State.Status,
        });
}
