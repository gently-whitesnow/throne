using Throne.Domain.Instructions;

namespace Throne.Domain.PromptParts;

/// <summary>
/// Operator-authored optional unit of prompt text for the embedded terminal (ADR-0035).
/// Carries a stable key, canonical text with a monotonic version counter, and per-mode
/// roles. Unlike <see cref="Instruction"/> it keeps no append-only text history — parts
/// are short and their edits are not dream-patch targets.
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
        EnsureModeRoles(modeRoles);
        return new PromptPart(id, key, scope, text, NormalizeDescription(description), currentVersion, modeRoles, createdAt, updatedAt);
    }

    /// <summary>
    /// Replace a unique substring of <see cref="Text"/>. Mirrors
    /// <see cref="Instruction.ReplaceText"/> semantics but bumps only the version counter.
    /// </summary>
    public ReplacePromptPartTextResult ReplaceText(string oldText, string newText, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
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
        return new ReplacePromptPartTextResult.Replaced();
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
        if (!InstructionScopeNames.IsKnown(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), $"Unknown prompt part scope: {scope}.");
        }
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
