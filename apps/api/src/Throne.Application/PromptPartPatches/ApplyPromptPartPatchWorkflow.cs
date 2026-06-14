using Throne.Application.Manifest;
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
        CancellationToken ct) => patch.Operation switch
        {
            PromptPartPatchOperationNames.ReplaceText => part is null
                ? ApplyAsInitialCreateAsync(patch, newText, null, now, ct)
                : ApplyOverExistingAsync(patch, part, newText, now, ct),
            PromptPartPatchOperationNames.Create => ApplyCreateAsync(patch, part, newText, now, ct),
            PromptPartPatchOperationNames.SetRoles => ApplySetRolesAsync(patch, part, now, ct),
            PromptPartPatchOperationNames.Delete => ApplyDeleteAsync(patch, part, now, ct),
            _ => throw PromptPartPatchExceptions.ValidationFailed(
                "operation",
                $"Unknown operation: {patch.Operation}."),
        };

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
        IReadOnlyList<PromptPartModeRole>? requestedModeRoles,
        DateTimeOffset now,
        CancellationToken ct) =>
        unitOfWork.ExecuteAsync<PromptPartPatch>(async inner =>
        {
            var modeRoles = requestedModeRoles
                ?? PromptPartManifestRoles.MandatoryRolesFor(
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
            var createOutcome = await promptParts.CreateAsync(part, initialVersion, inner);
            if (createOutcome is CreatePromptPartOutcome.KeyConflict)
            {
                throw PromptPartPatchExceptions.NeedsRebase(patch, currentVersion: 1);
            }

            return await PersistApplyAsync(patch, newText, part.CurrentVersion, now, inner);
        }, ct);

    private Task<PromptPartPatch> ApplyCreateAsync(
        PromptPartPatch patch,
        PromptPart? part,
        string newText,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (part is not null)
        {
            throw PromptPartPatchExceptions.NeedsRebase(patch, part.CurrentVersion);
        }
        return ApplyAsInitialCreateAsync(patch, newText, patch.ModeRoles, now, ct);
    }

    private Task<PromptPartPatch> ApplySetRolesAsync(
        PromptPartPatch patch,
        PromptPart? part,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (part is null)
        {
            throw PromptPartPatchExceptions.PartNotFound(patch.Identity.TargetScope, patch.Identity.TargetKey);
        }
        return unitOfWork.ExecuteAsync<PromptPartPatch>(async inner =>
        {
            var updatedPart = await promptParts.SetModeRolesAsync(
                part.Id,
                patch.ModeRoles ?? [],
                now,
                inner) ?? throw PromptPartPatchExceptions.PartNotFound(
                    patch.Identity.TargetScope,
                    patch.Identity.TargetKey);
            return await PersistApplyAsync(patch, patch.PatchText, updatedPart.CurrentVersion, now, inner);
        }, ct);
    }

    private Task<PromptPartPatch> ApplyDeleteAsync(
        PromptPartPatch patch,
        PromptPart? part,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (part is null)
        {
            throw PromptPartPatchExceptions.PartNotFound(patch.Identity.TargetScope, patch.Identity.TargetKey);
        }
        return unitOfWork.ExecuteAsync<PromptPartPatch>(async inner =>
        {
            await promptParts.SetModeRolesAsync(part.Id, [], now, inner);
            var outcome = await promptParts.DeleteAsync(part.Id, inner);
            if (outcome is DeletePromptPartOutcome.NotFound)
            {
                throw PromptPartPatchExceptions.PartNotFound(patch.Identity.TargetScope, patch.Identity.TargetKey);
            }
            return await PersistApplyAsync(patch, patch.PatchText, part.CurrentVersion, now, inner);
        }, ct);
    }

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
