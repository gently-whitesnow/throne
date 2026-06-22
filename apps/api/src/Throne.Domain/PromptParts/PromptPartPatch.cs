namespace Throne.Domain.PromptParts;

/// <summary>
/// Patch proposal against a single PromptPart (identified by <c>(scope, key)</c>), produced
/// by a frontier agent and decided by the operator (apply / apply-with-edit / reject).
/// ADR-0036 supersedes the legacy InstructionPatch.
///
/// Domain invariants:
///   • <see cref="PatchText"/> is the agent's original proposal — never mutated;
///   • <see cref="PromptPartPatchState.AppliedText"/> is non-null iff the user
///     applied the patch (verbatim → equal to <see cref="PatchText"/>; edited → divergent);
///   • <see cref="PromptPartPatchState.RejectComment"/> ≥ 10 chars after trimming and is
///     required for <see cref="PromptPartPatchStatusNames.Rejected"/>;
///   • <see cref="PromptPartPatchState.AppliedVersion"/> is the post-replace
///     <c>PromptPart.current_version</c> (= base + 1) and is set only by the apply path;
///   • <see cref="Rationale"/> ≤ 500 characters.
///
/// State transitions are exposed as instance methods (<see cref="Apply"/>,
/// <see cref="Reject"/>) which return the corresponding result enum without throwing —
/// callers translate the outcome into protocol-level errors.
/// </summary>
public sealed class PromptPartPatch
{
    public const int MinRejectCommentLength = 10;
    public const int MaxRationaleLength = 500;
    public const int MaxPatchTextLength = 32_000;

    public enum ApplyResult
    {
        Ok,
        AlreadyDecided,
        InvalidAppliedVersion,
    }

    public enum RejectResult
    {
        Ok,
        AlreadyDecided,
        CommentTooShort,
    }

    private PromptPartPatch(
        PromptPartPatchIdentity identity,
        PromptPartPatchState state,
        string operation,
        string patchText,
        IReadOnlyList<PromptPartModeRole>? modeRoles,
        string rationale)
    {
        Identity = identity;
        State = state;
        Operation = operation;
        PatchText = patchText;
        ModeRoles = modeRoles is null ? null : [.. modeRoles];
        Rationale = rationale;
    }

    public PromptPartPatchIdentity Identity { get; }
    public string Operation { get; }
    public string PatchText { get; }
    public IReadOnlyList<PromptPartModeRole>? ModeRoles { get; }
    public string Rationale { get; }
    public PromptPartPatchState State { get; private set; }

    public static PromptPartPatch Create(
        string id,
        string targetScope,
        string targetKey,
        string operation,
        string patchText,
        IReadOnlyList<PromptPartModeRole>? modeRoles,
        string rationale,
        int baseVersion,
        DateTimeOffset now)
    {
        EnsureRequiredStringsForCreate(id, targetScope, targetKey, rationale);
        ArgumentNullException.ThrowIfNull(patchText);
        EnsurePatchableScope(targetScope);
        EnsureOperationPayload(operation, patchText, modeRoles);
        PromptPartPatchBudgets.EnsureAll(patchText, rationale, baseVersion);

        var identity = new PromptPartPatchIdentity(id, targetScope, targetKey, baseVersion, now);
        return new PromptPartPatch(
            identity,
            PromptPartPatchState.Initial(now),
            operation,
            patchText,
            modeRoles,
            rationale);
    }

    public static PromptPartPatch Create(
        string id,
        string targetScope,
        string targetKey,
        string patchText,
        string rationale,
        int baseVersion,
        DateTimeOffset now) =>
        Create(
            id,
            targetScope,
            targetKey,
            PromptPartPatchOperationNames.ReplaceText,
            patchText,
            modeRoles: null,
            rationale,
            baseVersion,
            now);

    public static PromptPartPatch Restore(
        PromptPartPatchIdentity identity,
        PromptPartPatchState state,
        string? operation,
        string patchText,
        IReadOnlyList<PromptPartModeRole>? modeRoles,
        string rationale)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Id);
        EnsurePatchableScope(identity.TargetScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.TargetKey);
        EnsureKnownStatus(state.Status);
        var restoredOperation = string.IsNullOrWhiteSpace(operation)
            ? PromptPartPatchOperationNames.ReplaceText
            : operation;
        EnsureOperationPayload(restoredOperation, patchText, modeRoles);
        return new PromptPartPatch(identity, state, restoredOperation, patchText, modeRoles, rationale);
    }

    public static PromptPartPatch Restore(
        PromptPartPatchIdentity identity,
        PromptPartPatchState state,
        string patchText,
        string rationale) =>
        Restore(identity, state, null, patchText, null, rationale);

    /// <summary>
    /// User-driven apply transition. Verbatim apply (<paramref name="editedText"/>
    /// null or equal to <see cref="PatchText"/>) flips status to
    /// <see cref="PromptPartPatchStatusNames.Applied"/>; a divergent
    /// <paramref name="editedText"/> flips to
    /// <see cref="PromptPartPatchStatusNames.AppliedEdited"/>.
    /// </summary>
    public ApplyResult Apply(string? editedText, int appliedVersion, DateTimeOffset now)
    {
        if (State.Status != PromptPartPatchStatusNames.Proposed)
        {
            return ApplyResult.AlreadyDecided;
        }
        var minAppliedVersion = Operation == PromptPartPatchOperationNames.ReplaceText
            || Operation == PromptPartPatchOperationNames.Create
            ? Identity.BaseVersion + 1
            : Identity.BaseVersion;
        if (appliedVersion < minAppliedVersion)
        {
            return ApplyResult.InvalidAppliedVersion;
        }

        var (status, appliedText) = ResolveApplied(editedText, PatchText);
        State = new PromptPartPatchState(
            Status: status,
            AppliedText: appliedText,
            RejectComment: State.RejectComment,
            AppliedVersion: appliedVersion,
            UpdatedAt: now,
            DecidedAt: now);
        return ApplyResult.Ok;
    }

    /// <summary>
    /// User-driven reject transition. Requires a non-empty <paramref name="comment"/>
    /// with at least <see cref="MinRejectCommentLength"/> characters after trimming.
    /// </summary>
    public RejectResult Reject(string comment, DateTimeOffset now)
    {
        if (State.Status != PromptPartPatchStatusNames.Proposed)
        {
            return RejectResult.AlreadyDecided;
        }
        var trimmed = (comment ?? string.Empty).Trim();
        if (trimmed.Length < MinRejectCommentLength)
        {
            return RejectResult.CommentTooShort;
        }
        State = new PromptPartPatchState(
            Status: PromptPartPatchStatusNames.Rejected,
            AppliedText: State.AppliedText,
            RejectComment: trimmed,
            AppliedVersion: State.AppliedVersion,
            UpdatedAt: now,
            DecidedAt: now);
        return RejectResult.Ok;
    }

    private static (string Status, string AppliedText) ResolveApplied(string? editedText, string patchText)
    {
        if (editedText is null || string.Equals(editedText, patchText, StringComparison.Ordinal))
        {
            return (PromptPartPatchStatusNames.Applied, patchText);
        }
        return (PromptPartPatchStatusNames.AppliedEdited, editedText);
    }

    private static void EnsureRequiredStringsForCreate(
        string id,
        string targetScope,
        string targetKey,
        string rationale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetScope);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
    }

    private static void EnsurePatchableScope(string scope)
    {
        if (!PromptPartScopeNames.IsKnown(scope))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scope),
                $"Unknown prompt part scope: {scope}.");
        }
        if (!string.Equals(scope, PromptPartScopeNames.User, StringComparison.Ordinal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(scope),
                "PromptPartPatch targets must use scope='user'. System prompt parts are manifest-managed.");
        }
    }

    private static void EnsureKnownStatus(string status)
    {
        if (!PromptPartPatchStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                $"Unknown PromptPartPatch status: {status}.");
        }
    }

    private static void EnsureOperationPayload(
        string operation,
        string patchText,
        IReadOnlyList<PromptPartModeRole>? modeRoles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!PromptPartPatchOperationNames.IsKnown(operation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                $"Unknown PromptPartPatch operation: {operation}.");
        }
        if ((operation == PromptPartPatchOperationNames.ReplaceText
             || operation == PromptPartPatchOperationNames.Create)
            && string.IsNullOrWhiteSpace(patchText))
        {
            throw new ArgumentException("patch_text must not be empty for text operations.", nameof(patchText));
        }
        if (operation == PromptPartPatchOperationNames.Create
            || operation == PromptPartPatchOperationNames.SetRoles)
        {
            ArgumentNullException.ThrowIfNull(modeRoles);
            PromptPart.ValidateModeRoles(modeRoles);
        }
    }
}
