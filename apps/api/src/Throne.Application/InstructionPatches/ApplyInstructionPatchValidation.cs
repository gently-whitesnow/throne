using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Pre-flight checks for <see cref="ApplyInstructionPatchHandler"/>. Pure —
/// only operates on the values passed in, no dependencies, so static is appropriate.
/// </summary>
internal static class ApplyInstructionPatchValidation
{
    public static void EnsureBaseVersionMatches(InstructionPatch patch, Instruction? instruction)
    {
        if (instruction is null && patch.Identity.BaseInstructionVersion != 0)
        {
            throw InstructionPatchExceptions.InstructionNotFound(patch.Identity.TargetKind);
        }
        if (instruction is not null && instruction.CurrentVersion != patch.Identity.BaseInstructionVersion)
        {
            throw InstructionPatchExceptions.NeedsRebase(patch, instruction.CurrentVersion);
        }
    }
}
