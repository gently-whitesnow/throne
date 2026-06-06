namespace Throne.Domain.Instructions;

/// <summary>
/// Immutable identity tuple for <see cref="InstructionPatch"/> — the stable
/// coordinates (id, owner, target kind, base instruction version, created-at)
/// fixed at creation and never mutated thereafter.
/// </summary>
public sealed record InstructionPatchIdentity(
    string Id,
    string OwnerUserId,
    string TargetKind,
    int BaseInstructionVersion,
    DateTimeOffset CreatedAt);
