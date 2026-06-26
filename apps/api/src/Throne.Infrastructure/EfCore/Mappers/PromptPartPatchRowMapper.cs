using Throne.Domain.PromptParts;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class PromptPartPatchRowMapper
{
    public static PromptPartPatchRow ToRow(PromptPartPatch patch, string? idempotencyKey = null) => new()
    {
        Id = patch.Identity.Id,
        TargetScope = patch.Identity.TargetScope,
        TargetKey = patch.Identity.TargetKey,
        Status = patch.State.Status,
        Operation = patch.Operation,
        PatchText = patch.PatchText,
        ModeRoles = patch.ModeRoles?.Select(PromptPartRowMapper.ToPayload).ToList(),
        AppliedText = patch.State.AppliedText,
        Rationale = patch.Rationale,
        RejectComment = patch.State.RejectComment,
        BaseVersion = patch.Identity.BaseVersion,
        AppliedVersion = patch.State.AppliedVersion,
        CreatedAt = patch.Identity.CreatedAt,
        UpdatedAt = patch.State.UpdatedAt,
        DecidedAt = patch.State.DecidedAt,
        IdempotencyKey = idempotencyKey,
    };

    public static PromptPartPatch ToDomain(PromptPartPatchRow row) => PromptPartPatch.Restore(
        identity: new PromptPartPatchIdentity(
            Id: row.Id,
            TargetScope: row.TargetScope,
            TargetKey: row.TargetKey,
            BaseVersion: row.BaseVersion,
            CreatedAt: row.CreatedAt),
        state: new PromptPartPatchState(
            Status: row.Status,
            AppliedText: row.AppliedText,
            RejectComment: row.RejectComment,
            AppliedVersion: row.AppliedVersion,
            UpdatedAt: row.UpdatedAt,
            DecidedAt: row.DecidedAt),
        operation: row.Operation,
        patchText: row.PatchText,
        modeRoles: row.ModeRoles?.Select(PromptPartRowMapper.ToDomainRole).ToList(),
        rationale: row.Rationale ?? string.Empty);
}
