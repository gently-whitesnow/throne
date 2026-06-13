using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

public sealed record ApplyPromptPartPatchCommand(string PatchId, string? FinalText);

/// <summary>
/// User-driven apply path. Modes:
///   * verbatim — <c>final_text</c> omitted or equal to <c>patch_text</c>;
///   * with edit — <c>final_text</c> differs (status becomes <c>applied_edited</c>,
///     <c>applied_text</c> stores the user's text).
///
/// Validates <c>base_version</c> against the live PromptPart; mismatch surfaces as
/// <c>prompt_part_patch.needs_rebase</c> without mutation. On success the entire
/// PromptPart.text is replaced (single-shot replace) and the patch is marked applied.
/// </summary>
public sealed class ApplyPromptPartPatchHandler(
    IPromptPartPatchRepository patches,
    UserPromptPartLookup partLookup,
    ApplyPromptPartPatchWorkflow workflow,
    TimeProvider clock)
{
    public async Task<PromptPartPatch> HandleAsync(ApplyPromptPartPatchCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var patch = await patches.GetAsync(command.PatchId, ct)
            ?? throw PromptPartPatchExceptions.NotFound(command.PatchId);
        if (patch.State.Status != PromptPartPatchStatusNames.Proposed)
        {
            throw PromptPartPatchExceptions.AlreadyDecided(patch);
        }

        var part = await partLookup.FindAsync(patch.Identity.TargetScope, patch.Identity.TargetKey, ct);
        ApplyPromptPartPatchValidation.EnsureBaseVersionMatches(patch, part);

        var newText = command.FinalText ?? patch.PatchText;
        var now = clock.GetUtcNow();
        return await workflow.ExecuteAsync(patch, part, newText, now, ct);
    }
}
