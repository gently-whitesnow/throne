using Throne.Domain.TextVersions;

namespace Throne.Domain.Instructions;

/// <summary>
/// User- or system-scoped instruction that drives a frontier agent. Holds the
/// canonical text plus its monotonically increasing version number; the full
/// history is replayed from <see cref="TextVersion"/> entries persisted by
/// the repository.
/// </summary>
public sealed class Instruction
{
    private Instruction(
        InstructionId id,
        InstructionDescriptor descriptor,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Descriptor = descriptor;
        Text = text;
        CurrentVersion = currentVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public InstructionId Id { get; }
    public InstructionDescriptor Descriptor { get; }
    public string Text { get; private set; }
    public int CurrentVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Instruction Create(
        InstructionId id,
        string scope,
        string kind,
        string text,
        DateTimeOffset now)
    {
        EnsureCreateInputs(scope, kind, text);
        return new Instruction(id, new InstructionDescriptor(scope, kind), text, currentVersion: 1, now, now);
    }

    public static Instruction Restore(
        InstructionId id,
        string scope,
        string kind,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        EnsureCreateInputs(scope, kind, text);
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }
        return new Instruction(id, new InstructionDescriptor(scope, kind), text, currentVersion, createdAt, updatedAt);
    }

    /// <summary>
    /// Apply a textual replace-edit. The previous text must be exactly
    /// <paramref name="oldText"/> at one position; empty <paramref name="oldText"/>
    /// is allowed only for the initial-fill case (<see cref="Text"/> is empty).
    /// </summary>
    public ReplaceInstructionTextResult ReplaceText(
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
            return new ReplaceInstructionTextResult.MatchNotFound(TextEditMatcher.BuildQueryPreview(oldText));
        }
        if (indices.Count > 1)
        {
            return new ReplaceInstructionTextResult.MatchAmbiguous(
                indices.Count,
                TextEditLineLookup.ToMatchLines(Text, indices, limit: 5));
        }

        var index = indices[0];
        Text = string.Concat(
            Text.AsSpan(0, index),
            newText,
            Text.AsSpan(index + oldText.Length));
        CurrentVersion += 1;
        UpdatedAt = now;

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Instruction,
            OwnerId: Id.Value,
            Version: CurrentVersion,
            Kind: TextVersionKind.Replace,
            Delta: new TextVersionDelta(null, oldText, newText, null, null),
            ChangedAt: now,
            ChangedBy: changedBy);

        return new ReplaceInstructionTextResult.Replaced(version);
    }

    private static void EnsureCreateInputs(string scope, string kind, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(text);
        if (!InstructionScopeNames.IsKnown(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), $"Unknown instruction scope: {scope}.");
        }
        if (!InstructionKindNames.IsKnown(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), $"Unknown instruction kind: {kind}.");
        }
    }
}
