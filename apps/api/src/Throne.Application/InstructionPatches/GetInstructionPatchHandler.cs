using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

public sealed class GetInstructionPatchHandler(
    IInstructionPatchRepository patches,
    UserInstructionLookup userInstructions,
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

        return InstructionPatchView.From(patch, instruction);
    }
}

public sealed record InstructionPatchView(
    InstructionPatch Patch,
    string CurrentInstructionText,
    int CurrentInstructionVersion,
    bool BaseVersionMatchesCurrent)
{
    public static InstructionPatchView From(InstructionPatch patch, Instruction? instruction) => new(
        patch,
        instruction?.Text ?? string.Empty,
        instruction?.CurrentVersion ?? 0,
        instruction is not null && instruction.CurrentVersion == patch.Identity.BaseInstructionVersion);
}
