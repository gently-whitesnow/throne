namespace Throne.Domain.Instructions;

/// <summary>
/// State snapshot for <see cref="InstructionPatch"/> persistence. Lives next to
/// <see cref="InstructionPatchIdentity"/> so factory and Restore methods stay
/// short.
/// </summary>
public sealed record InstructionPatchState(
    string Status,
    string? AppliedText,
    string? RejectComment,
    int? AppliedInstructionVersion,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DecidedAt)
{
    /// <summary>
    /// Default state for a freshly-created patch in
    /// <see cref="InstructionPatchStatusNames.Proposed"/>.
    /// </summary>
    public static InstructionPatchState Initial(DateTimeOffset now) =>
        new(InstructionPatchStatusNames.Proposed, null, null, null, now, null);
}
