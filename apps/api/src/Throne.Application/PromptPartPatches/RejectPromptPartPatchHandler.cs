using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

public sealed record RejectPromptPartPatchCommand(string PatchId, string Comment);

/// <summary>
/// User-driven reject path with a mandatory comment (≥10 chars after trimming). The comment is
/// part of the patch's persistent state — the next analysis round must take it into account so
/// the same proposal is not re-emitted.
/// </summary>
public sealed class RejectPromptPartPatchHandler(
    IPromptPartPatchRepository patches,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<PromptPartPatch> HandleAsync(RejectPromptPartPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patch = await patches.GetAsync(command.PatchId, ct)
            ?? throw PromptPartPatchExceptions.NotFound(command.PatchId);

        var transition = patch.Reject(command.Comment, clock.GetUtcNow());
        PromptPartPatchOutcomeMapper.ThrowForRejectTransition(transition, patch);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => patches.RejectAsync(patch, inner),
            ct);

        return PromptPartPatchOutcomeMapper.UnwrapReject(outcome, command.PatchId);
    }
}
