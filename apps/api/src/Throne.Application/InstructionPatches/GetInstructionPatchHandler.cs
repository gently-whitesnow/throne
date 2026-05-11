using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

public sealed class GetInstructionPatchHandler(
    IInstructionPatchRepository patches,
    IInstructionRepository instructions,
    ICurrentUserAccessor currentUser)
{
    public async Task<InstructionPatchView> HandleAsync(string patchId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(patchId);

        var patch = await patches.GetAsync(patchId, ct)
            ?? throw NotFound(patchId);
        if (!string.Equals(patch.OwnerUserId, currentUser.UserId, StringComparison.Ordinal))
        {
            throw NotFound(patchId);
        }

        var instructionList = await instructions.GetUserInstructionsByKindsAsync(
            patch.OwnerUserId,
            [patch.TargetKind],
            ct);
        var instruction = instructionList.Count == 0 ? null : instructionList[0];

        return new InstructionPatchView(
            patch,
            instruction?.Text ?? string.Empty,
            instruction?.CurrentVersion ?? 0,
            instruction is not null && instruction.CurrentVersion == patch.BaseInstructionVersion);
    }

    private static ApiException NotFound(string patchId) => new(
        ErrorCodes.InstructionPatchNotFound,
        $"InstructionPatch '{patchId}' not found.",
        new Dictionary<string, object?> { ["patch_id"] = patchId });
}

public sealed record InstructionPatchView(
    InstructionPatch Patch,
    string CurrentInstructionText,
    int CurrentInstructionVersion,
    bool BaseVersionMatchesCurrent);
