using Throne.Domain.TextVersions;

namespace Throne.Domain.PromptParts;

/// <summary>
/// Unified prompt part (ADR-0036 collapses the legacy Instruction aggregate into this).
/// Carries a stable key (unique within scope), canonical text with a monotonic version
/// counter, per-mode roles, and an append-only text history (replayed from
/// <see cref="TextVersion"/> entries persisted by the repository). Mandatory parts back
/// the agent-visible bundle; optional parts drive the embedded terminal composition.
/// </summary>
public sealed class PromptPart
{
    private readonly List<PromptPartModeRole> _modeRoles;

    private PromptPart(
        PromptPartId id,
        string key,
        string scope,
        string text,
        string? description,
        int currentVersion,
        IEnumerable<PromptPartModeRole> modeRoles,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Key = key;
        Scope = scope;
        Text = text;
        Description = description;
        CurrentVersion = currentVersion;
        _modeRoles = modeRoles.ToList();
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public PromptPartId Id { get; }
    public string Key { get; }
    public string Scope { get; }
    public string Text { get; private set; }
    public string? Description { get; private set; }
    public int CurrentVersion { get; private set; }
    public IReadOnlyList<PromptPartModeRole> ModeRoles => _modeRoles;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static PromptPart Create(
        PromptPartId id,
        string scope,
        string key,
        string text,
        string? description,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset now)
    {
        EnsureScopeAndKey(scope, key);
        ArgumentNullException.ThrowIfNull(text);
        EnsureModeRoles(modeRoles);
        return new PromptPart(id, key, scope, text, NormalizeDescription(description), currentVersion: 1, modeRoles, now, now);
    }

    public static PromptPart Restore(
        PromptPartId id,
        string scope,
        string key,
        string text,
        string? description,
        int currentVersion,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        EnsureScopeAndKey(scope, key);
        ArgumentNullException.ThrowIfNull(text);
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }
        // Tolerant rehydration: a mode retired from the code after the row was written
        // (e.g. a removed bundle mode) must NOT poison hydration of the whole aggregate —
        // such roles are dropped, not thrown on. Command paths (Create/SetModeRoles/
        // ValidateModeRoles) stay strict and still reject unknown modes from callers; the
        // seed reconcile re-derives the desired roles afterwards.
        var liveModeRoles = DropRetiredModes(modeRoles);
        EnsureModeRoles(liveModeRoles);
        return new PromptPart(id, key, scope, text, NormalizeDescription(description), currentVersion, liveModeRoles, createdAt, updatedAt);
    }

    /// <summary>
    /// Apply a textual replace-edit and append a <see cref="TextVersion"/> delta. The
    /// previous text must be exactly <paramref name="oldText"/> at one position; empty
    /// <paramref name="oldText"/> is allowed only for the initial-fill case
    /// (<see cref="Text"/> is empty). Mirrors the legacy <c>Instruction.ReplaceText</c>
    /// semantics (match-not-found / ambiguous).
    /// </summary>
    public ReplacePromptPartTextResult ReplaceText(
        string oldText,
        string newText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentException.ThrowIfNullOrEmpty(newVersionId);
        if (oldText.Length == 0 && Text.Length != 0)
        {
            throw new ArgumentException(
                "old_text may be empty only when current text is empty (initial fill).",
                nameof(oldText));
        }

        var indices = TextEditMatcher.FindAllIndices(Text, oldText);
        if (indices.Count == 0)
        {
            return new ReplacePromptPartTextResult.MatchNotFound(TextEditMatcher.BuildQueryPreview(oldText));
        }
        if (indices.Count > 1)
        {
            return new ReplacePromptPartTextResult.MatchAmbiguous(
                indices.Count,
                TextEditLineLookup.ToMatchLines(Text, indices, limit: 5));
        }

        var index = indices[0];
        Text = string.Concat(Text.AsSpan(0, index), newText, Text.AsSpan(index + oldText.Length));
        CurrentVersion += 1;
        UpdatedAt = now;

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.PromptPart,
            OwnerId: Id.Value,
            Version: CurrentVersion,
            Kind: TextVersionKind.Replace,
            Delta: new TextVersionDelta(null, oldText, newText, null, null),
            ChangedAt: now,
            ChangedBy: changedBy);

        return new ReplacePromptPartTextResult.Replaced(version);
    }

    /// <summary>Validates a mode-role set without an aggregate instance (e.g. for request validation).</summary>
    public static void ValidateModeRoles(IReadOnlyList<PromptPartModeRole> modeRoles) => EnsureModeRoles(modeRoles);

    public void SetModeRoles(IReadOnlyList<PromptPartModeRole> modeRoles, DateTimeOffset now)
    {
        EnsureModeRoles(modeRoles);
        _modeRoles.Clear();
        _modeRoles.AddRange(modeRoles);
        UpdatedAt = now;
    }

    private static void EnsureScopeAndKey(string scope, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!PromptPartScopeNames.IsKnown(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), $"Unknown prompt part scope: {scope}.");
        }
    }

    /// <summary>
    /// Drops mode-roles whose mode is no longer known to the code. Used only on the
    /// restore-from-store path (see <see cref="Restore"/>); never on command paths.
    /// </summary>
    private static List<PromptPartModeRole> DropRetiredModes(IReadOnlyList<PromptPartModeRole> modeRoles)
    {
        ArgumentNullException.ThrowIfNull(modeRoles);
        return modeRoles
            .Where(r => r is not null && PromptPartModeNames.IsKnown(r.Mode))
            .ToList();
    }

    private static void EnsureModeRoles(IReadOnlyList<PromptPartModeRole> modeRoles)
    {
        ArgumentNullException.ThrowIfNull(modeRoles);
        var seenModes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in modeRoles)
        {
            ArgumentNullException.ThrowIfNull(role);
            if (!PromptPartModeNames.IsKnown(role.Mode))
            {
                throw new ArgumentOutOfRangeException(nameof(modeRoles), $"Unknown prompt part mode: {role.Mode}.");
            }
            if (!PromptPartRoleNames.IsKnown(role.Role))
            {
                throw new ArgumentOutOfRangeException(nameof(modeRoles), $"Unknown prompt part role: {role.Role}.");
            }
            if (role.Order < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(modeRoles), "Prompt part role order must be >= 0.");
            }
            if (!seenModes.Add(role.Mode))
            {
                throw new ArgumentException($"Duplicate role for mode '{role.Mode}'.", nameof(modeRoles));
            }
        }
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description;
}
