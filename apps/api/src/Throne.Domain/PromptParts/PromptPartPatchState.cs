namespace Throne.Domain.PromptParts;

/// <summary>
/// State snapshot for <see cref="PromptPartPatch"/> persistence. Lives next to
/// <see cref="PromptPartPatchIdentity"/> so factory and Restore methods stay short.
/// </summary>
public sealed record PromptPartPatchState(
    string Status,
    string? AppliedText,
    string? RejectComment,
    int? AppliedVersion,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DecidedAt)
{
    /// <summary>
    /// Default state for a freshly-created patch in
    /// <see cref="PromptPartPatchStatusNames.Proposed"/>.
    /// </summary>
    public static PromptPartPatchState Initial(DateTimeOffset now) =>
        new(PromptPartPatchStatusNames.Proposed, null, null, null, now, null);
}
