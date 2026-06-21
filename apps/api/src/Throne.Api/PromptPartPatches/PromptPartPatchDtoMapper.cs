using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;
using Throne.PromptPartPatches.Contracts.Generated;

namespace Throne.Api.PromptPartPatches;

/// <summary>
/// Boundary mapper between <see cref="PromptPartPatch"/> and the OpenAPI DTOs.
/// Realtime fanout reuses <see cref="ToDto"/> through <c>RealtimeDomainEventHandler</c>.
/// </summary>
public static class PromptPartPatchDtoMapper
{
    public static PromptPartPatchDto ToDto(PromptPartPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var dto = new PromptPartPatchDto
        {
            Id = patch.Identity.Id,
            Target_scope = patch.Identity.TargetScope,
            Target_key = patch.Identity.TargetKey,
            Status = ToStatus(patch.State.Status),
            Operation = ToOperation(patch.Operation),
            Patch_text = patch.PatchText,
            Mode_roles = patch.ModeRoles?.Select(ToRoleDto).ToList(),
            Applied_text = patch.State.AppliedText,
            Evidence_card_ids = patch.EvidenceCardIds.ToList(),
            Rationale = patch.Rationale,
            Reject_comment = patch.State.RejectComment,
            Base_version = patch.Identity.BaseVersion,
            Created_at = patch.Identity.CreatedAt,
            Updated_at = patch.State.UpdatedAt,
        };
        if (patch.State.AppliedVersion is { } applied)
        {
            dto.Applied_version = applied;
        }
        if (patch.State.DecidedAt is { } decided)
        {
            dto.Decided_at = decided;
        }
        return dto;
    }

    public static PromptPartPatchPageDto ToPageDto(PromptPartPatchPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new PromptPartPatchPageDto
        {
            Items = page.Items.Select(ToDto).ToList(),
            Next_cursor = page.NextCursor,
        };
    }

    public static PromptPartPatchDetailDto ToDetailDto(PromptPartPatchView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new PromptPartPatchDetailDto
        {
            Patch = ToDto(view.Patch),
            Current_part_text = view.CurrentPartText,
            Current_part_version = view.CurrentPartVersion,
            Base_version_matches_current = view.BaseVersionMatchesCurrent,
            Base_part_text = view.BasePartText,
        };
    }

    public static PromptPartPatchStatus ToStatus(string status) => status switch
    {
        PromptPartPatchStatusNames.Proposed => PromptPartPatchStatus.Proposed,
        PromptPartPatchStatusNames.Applied => PromptPartPatchStatus.Applied,
        PromptPartPatchStatusNames.AppliedEdited => PromptPartPatchStatus.Applied_edited,
        PromptPartPatchStatusNames.Rejected => PromptPartPatchStatus.Rejected,
        PromptPartPatchStatusNames.Superseded => PromptPartPatchStatus.Superseded,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown patch status."),
    };

    public static string FromStatus(PromptPartPatchStatus status) => status switch
    {
        PromptPartPatchStatus.Proposed => PromptPartPatchStatusNames.Proposed,
        PromptPartPatchStatus.Applied => PromptPartPatchStatusNames.Applied,
        PromptPartPatchStatus.Applied_edited => PromptPartPatchStatusNames.AppliedEdited,
        PromptPartPatchStatus.Rejected => PromptPartPatchStatusNames.Rejected,
        PromptPartPatchStatus.Superseded => PromptPartPatchStatusNames.Superseded,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown patch status."),
    };

    public static string FromOperation(PromptPartPatchOperation operation) => operation switch
    {
        PromptPartPatchOperation.Replace_text => PromptPartPatchOperationNames.ReplaceText,
        PromptPartPatchOperation.Create => PromptPartPatchOperationNames.Create,
        PromptPartPatchOperation.Set_roles => PromptPartPatchOperationNames.SetRoles,
        PromptPartPatchOperation.Delete => PromptPartPatchOperationNames.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown patch operation."),
    };

    public static IReadOnlyList<PromptPartModeRole>? ToDomainRoles(
        ICollection<PromptPartModeRoleDto>? roles) =>
        roles?.Select(r => new PromptPartModeRole(r.Mode, r.Role, r.Order)).ToList();

    private static PromptPartPatchOperation ToOperation(string operation) => operation switch
    {
        PromptPartPatchOperationNames.ReplaceText => PromptPartPatchOperation.Replace_text,
        PromptPartPatchOperationNames.Create => PromptPartPatchOperation.Create,
        PromptPartPatchOperationNames.SetRoles => PromptPartPatchOperation.Set_roles,
        PromptPartPatchOperationNames.Delete => PromptPartPatchOperation.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown patch operation."),
    };

    private static PromptPartModeRoleDto ToRoleDto(PromptPartModeRole role) => new()
    {
        Mode = role.Mode,
        Role = role.Role,
        Order = role.Order,
    };
}
