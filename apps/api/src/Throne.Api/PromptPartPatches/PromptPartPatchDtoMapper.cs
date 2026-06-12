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
            Patch_text = patch.PatchText,
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
}
