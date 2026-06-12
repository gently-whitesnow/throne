using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Application.PromptPartPatches;

/// <summary>
/// The transactional core of the apply-patch operation. Lives outside
/// <see cref="ApplyPromptPartPatchHandler"/> so the per-type complexity budget of the handler
/// stays within threshold. When the target user part does not yet exist (base_version=0) it is
/// lazily created with the mandatory mode-roles derived from the manifest so it lands in the
/// right bundles by construction (ADR-0036).
/// </summary>
public sealed class ApplyPromptPartPatchWorkflow(
    IPromptPartPatchRepository patches,
    IPromptPartRepository promptParts,
    ISkillManifestProvider manifestProvider,
    IUnitOfWork unitOfWork)
{
    public Task<PromptPartPatch> ExecuteAsync(
        PromptPartPatch patch,
        PromptPart? part,
        string newText,
        DateTimeOffset now,
        CancellationToken ct) =>
        part is null
            ? ApplyAsInitialCreateAsync(patch, newText, now, ct)
            : ApplyOverExistingAsync(patch, part, newText, now, ct);

    private Task<PromptPartPatch> ApplyOverExistingAsync(
        PromptPartPatch patch,
        PromptPart part,
        string newText,
        DateTimeOffset now,
        CancellationToken ct) =>
        unitOfWork.ExecuteAsync<PromptPartPatch>(async inner =>
        {
            var replaceOutcome = await promptParts.ReplaceTextAsync(
                part.Id,
                patch.Identity.BaseVersion,
                part.Text,
                newText,
                TextVersionAuthor.User,
                now,
                inner);
            var updatedPart = PromptPartPatchOutcomeMapper.UnwrapReplace(
                replaceOutcome, patch, part.Id.Value);

            return await PersistApplyAsync(patch, newText, updatedPart.CurrentVersion, now, inner);
        }, ct);

    private Task<PromptPartPatch> ApplyAsInitialCreateAsync(
        PromptPartPatch patch,
        string newText,
        DateTimeOffset now,
        CancellationToken ct) =>
        unitOfWork.ExecuteAsync<PromptPartPatch>(async inner =>
        {
            var modeRoles = PromptPartManifestRoles.MandatoryRolesFor(
                patch.Identity.TargetScope, patch.Identity.TargetKey, manifestProvider.Current);
            var part = PromptPart.Create(
                id: PromptPartId.New(),
                scope: patch.Identity.TargetScope,
                key: patch.Identity.TargetKey,
                text: newText,
                description: null,
                modeRoles: modeRoles,
                now: now);
            var initialVersion = TextVersion.CreateSnapshot(
                id: Guid.NewGuid().ToString("N"),
                ownerKind: TextVersionOwnerKind.PromptPart,
                ownerId: part.Id.Value,
                snapshot: part.Text,
                changedAt: now,
                changedBy: TextVersionAuthor.User);
            await promptParts.CreateAsync(part, initialVersion, inner);

            return await PersistApplyAsync(patch, newText, part.CurrentVersion, now, inner);
        }, ct);

    private async Task<PromptPartPatch> PersistApplyAsync(
        PromptPartPatch patch,
        string newText,
        int appliedVersion,
        DateTimeOffset now,
        CancellationToken ct)
    {
        patch.Apply(newText, appliedVersion, now);
        var outcome = await patches.ApplyAsync(patch, ct);
        return PromptPartPatchOutcomeMapper.UnwrapApply(outcome, patch.Identity.Id);
    }
}
