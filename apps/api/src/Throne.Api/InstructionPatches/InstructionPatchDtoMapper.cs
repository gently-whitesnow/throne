using Throne.Application.InstructionPatches;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.InstructionPatches.Contracts.Generated;

namespace Throne.Api.InstructionPatches;

/// <summary>
/// Boundary mapper between <see cref="InstructionPatch"/> and the OpenAPI DTOs.
/// Realtime fanout reuses <see cref="ToDto"/> through
/// <c>RealtimeDomainEventHandler</c>.
/// </summary>
public static class InstructionPatchDtoMapper
{
    public static InstructionPatchDto ToDto(InstructionPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var dto = new InstructionPatchDto
        {
            Id = patch.Identity.Id,
            Target_kind = ToTargetKind(patch.Identity.TargetKind),
            Status = ToStatus(patch.State.Status),
            Patch_text = patch.PatchText,
            Applied_text = patch.State.AppliedText,
            Evidence_card_ids = patch.EvidenceCardIds.ToList(),
            Rationale = patch.Rationale,
            Reject_comment = patch.State.RejectComment,
            Base_instruction_version = patch.Identity.BaseInstructionVersion,
            Created_at = patch.Identity.CreatedAt,
            Updated_at = patch.State.UpdatedAt,
        };
        if (patch.State.AppliedInstructionVersion is { } applied)
        {
            dto.Applied_instruction_version = applied;
        }
        if (patch.State.DecidedAt is { } decided)
        {
            dto.Decided_at = decided;
        }
        return dto;
    }

    public static InstructionPatchPageDto ToPageDto(InstructionPatchPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new InstructionPatchPageDto
        {
            Items = page.Items.Select(ToDto).ToList(),
            Next_cursor = page.NextCursor,
        };
    }

    public static InstructionPatchDetailDto ToDetailDto(InstructionPatchView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new InstructionPatchDetailDto
        {
            Patch = ToDto(view.Patch),
            Current_instruction_text = view.CurrentInstructionText,
            Current_instruction_version = view.CurrentInstructionVersion,
            Base_version_matches_current = view.BaseVersionMatchesCurrent,
        };
    }

    public static InstructionPatchTargetKind ToTargetKind(string kind) => kind switch
    {
        InstructionKindNames.Common => InstructionPatchTargetKind.Common,
        InstructionKindNames.Interview => InstructionPatchTargetKind.Interview,
        InstructionKindNames.Work => InstructionPatchTargetKind.Work,
        InstructionKindNames.Dream => InstructionPatchTargetKind.Dream,
        InstructionKindNames.Transfer => InstructionPatchTargetKind.Transfer,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown instruction kind."),
    };

    public static InstructionPatchStatus ToStatus(string status) => status switch
    {
        InstructionPatchStatusNames.Proposed => InstructionPatchStatus.Proposed,
        InstructionPatchStatusNames.Applied => InstructionPatchStatus.Applied,
        InstructionPatchStatusNames.AppliedEdited => InstructionPatchStatus.Applied_edited,
        InstructionPatchStatusNames.Rejected => InstructionPatchStatus.Rejected,
        InstructionPatchStatusNames.Superseded => InstructionPatchStatus.Superseded,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown patch status."),
    };

    public static string FromTargetKind(InstructionPatchTargetKind kind) => kind switch
    {
        InstructionPatchTargetKind.Common => InstructionKindNames.Common,
        InstructionPatchTargetKind.Interview => InstructionKindNames.Interview,
        InstructionPatchTargetKind.Work => InstructionKindNames.Work,
        InstructionPatchTargetKind.Dream => InstructionKindNames.Dream,
        InstructionPatchTargetKind.Transfer => InstructionKindNames.Transfer,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown patch target kind."),
    };

    public static string FromStatus(InstructionPatchStatus status) => status switch
    {
        InstructionPatchStatus.Proposed => InstructionPatchStatusNames.Proposed,
        InstructionPatchStatus.Applied => InstructionPatchStatusNames.Applied,
        InstructionPatchStatus.Applied_edited => InstructionPatchStatusNames.AppliedEdited,
        InstructionPatchStatus.Rejected => InstructionPatchStatusNames.Rejected,
        InstructionPatchStatus.Superseded => InstructionPatchStatusNames.Superseded,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown patch status."),
    };
}
