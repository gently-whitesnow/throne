namespace Throne.Domain.Instructions;

/// <summary>
/// Immutable identity tuple for <see cref="InstructionPatch"/>. Keeping it as a
/// record reduces the aggregate's per-method cyclomatic complexity (CA1502
/// budget) by collapsing the constructor's argument count.
/// </summary>
public sealed record InstructionPatchIdentity(
    string Id,
    string OwnerUserId,
    string TargetKind,
    int BaseInstructionVersion,
    DateTimeOffset CreatedAt);
