using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.InstructionPatches;

public sealed class GetInstructionPatchHandler(
    IInstructionPatchRepository patches,
    UserInstructionLookup userInstructions,
    ITextVersionRepository textVersions,
    ICurrentUserAccessor currentUser)
{
    public async Task<InstructionPatchView> HandleAsync(string patchId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchId);

        var patch = await patches.GetAsync(patchId, ct)
            ?? throw InstructionPatchExceptions.NotFound(patchId);
        InstructionPatchOwnerGuard.EnsureOwner(patch, currentUser);

        var instruction = await userInstructions.FindAsync(
            patch.Identity.OwnerUserId,
            patch.Identity.TargetKind,
            ct);

        var baseText = await ReadBaseInstructionTextAsync(patch, instruction, ct);
        return InstructionPatchView.From(patch, instruction, baseText);
    }

    /// <summary>
    /// Replay the instruction's text-version history up to
    /// <c>patch.base_instruction_version</c> so the UI can show a clean diff
    /// `base → proposed` (or `base → applied`) for patches in any status.
    /// Returns empty string when there is no history yet
    /// (<c>base_instruction_version=0</c> means the patch creates the
    /// instruction from scratch) or when the underlying instruction was deleted.
    /// </summary>
    private async Task<string> ReadBaseInstructionTextAsync(
        InstructionPatch patch, Instruction? instruction, CancellationToken ct)
    {
        if (instruction is null || patch.Identity.BaseInstructionVersion <= 0)
        {
            return string.Empty;
        }
        if (patch.Identity.BaseInstructionVersion == instruction.CurrentVersion)
        {
            // Fast path: base text equals current text; no need to replay.
            return instruction.Text;
        }

        var versions = await textVersions.ListByOwnerAsync(
            TextVersionOwnerKind.Instruction, instruction.Id.Value, ct);
        return TextVersionReplay.ReplayTo(versions, patch.Identity.BaseInstructionVersion);
    }
}

public sealed record InstructionPatchView(
    InstructionPatch Patch,
    string CurrentInstructionText,
    int CurrentInstructionVersion,
    bool BaseVersionMatchesCurrent,
    string BaseInstructionText)
{
    public static InstructionPatchView From(InstructionPatch patch, Instruction? instruction, string baseInstructionText) => new(
        patch,
        instruction?.Text ?? string.Empty,
        instruction?.CurrentVersion ?? 0,
        instruction is not null && instruction.CurrentVersion == patch.Identity.BaseInstructionVersion,
        baseInstructionText);
}
