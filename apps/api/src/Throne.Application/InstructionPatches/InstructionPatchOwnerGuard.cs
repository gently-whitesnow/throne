using Throne.Application.Auth;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Ownership check for patch handlers. A foreign caller is mapped to NotFound
/// to avoid leaking existence of someone else's patch.
/// </summary>
internal static class InstructionPatchOwnerGuard
{
    public static void EnsureOwner(InstructionPatch patch, ICurrentUserAccessor currentUser)
    {
        if (!string.Equals(patch.Identity.OwnerUserId, currentUser.UserId, StringComparison.Ordinal))
        {
            throw InstructionPatchExceptions.NotFound(patch.Identity.Id);
        }
    }
}
