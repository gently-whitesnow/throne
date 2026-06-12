namespace Throne.Domain.PromptParts;

/// <summary>
/// Immutable identity tuple for <see cref="PromptPartPatch"/>. The target is a
/// <c>(scope, key)</c> pair (ADR-0036). Keeping it as a record reduces the aggregate's
/// per-method cyclomatic complexity by collapsing the constructor's argument count.
/// </summary>
public sealed record PromptPartPatchIdentity(
    string Id,
    string TargetScope,
    string TargetKey,
    int BaseVersion,
    DateTimeOffset CreatedAt);
