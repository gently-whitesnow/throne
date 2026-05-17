using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Reads the single user-scoped <see cref="Instruction"/> for a given target_kind.
/// Encapsulates the kinds-list call so handlers don't repeat the
/// "GetUserInstructionsByKindsAsync → first-or-null" pattern.
/// </summary>
public sealed class UserInstructionLookup(IInstructionRepository instructions)
{
    public async Task<Instruction?> FindAsync(string ownerUserId, string kind, CancellationToken ct)
    {
        var list = await instructions.GetUserInstructionsByKindsAsync(ownerUserId, [kind], ct);
        return list.Count == 0 ? null : list[0];
    }
}
