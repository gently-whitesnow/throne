using Throne.Domain.TextVersions;

namespace Throne.Domain.Instructions;

public sealed class Instruction
{
    private Instruction(
        InstructionId id,
        string scope,
        string? userId,
        string kind,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Scope = scope;
        UserId = userId;
        Kind = kind;
        Text = text;
        CurrentVersion = currentVersion;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public InstructionId Id { get; }
    public string Scope { get; }
    public string? UserId { get; }
    public string Kind { get; }
    public string Text { get; private set; }
    public int CurrentVersion { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Instruction Create(
        InstructionId id,
        string scope,
        string? userId,
        string kind,
        string text,
        DateTimeOffset now)
    {
        Validate(scope, userId, kind, text);
        return new Instruction(id, scope, userId, kind, text, currentVersion: 1, now, now);
    }

    public static Instruction Restore(
        InstructionId id,
        string scope,
        string? userId,
        string kind,
        string text,
        int currentVersion,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Validate(scope, userId, kind, text);
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        return new Instruction(id, scope, userId, kind, text, currentVersion, createdAt, updatedAt);
    }

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

        // Initial-fill для пустых user-антагонистов: old_text="" разрешён только когда текущий Text тоже пуст.
        // На непустом Text пустой old_text матчится в каждой позиции и ломает «exactly once» инвариант.
        if (oldText.Length == 0 && Text.Length != 0)
        {
            throw new ArgumentException(
                "old_text may be empty only when current text is empty (initial fill).",
                nameof(oldText));
        }

        var indices = FindAllIndices(Text, oldText);
        if (indices.Count == 0)
        {
            return new ReplaceInstructionTextResult.MatchNotFound(BuildQueryPreview(oldText));
        }

        if (indices.Count > 1)
        {
            return new ReplaceInstructionTextResult.MatchAmbiguous(
                indices.Count,
                ToMatchLines(Text, indices, limit: 5));
        }

        var index = indices[0];
        Text = string.Concat(Text.AsSpan(0, index), newText, Text.AsSpan(index + oldText.Length));
        CurrentVersion += 1;
        UpdatedAt = now;

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Instruction,
            OwnerId: Id.Value,
            Version: CurrentVersion,
            Kind: TextVersionKind.Replace,
            Snapshot: null,
            OldText: oldText,
            NewText: newText,
            AfterLine: null,
            InsertText: null,
            ChangedAt: now,
            ChangedBy: changedBy);

        return new ReplaceInstructionTextResult.Replaced(version);
    }

    private static void Validate(string scope, string? userId, string kind, string text)
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

        if (scope == InstructionScopeNames.User)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("user_id is required for user-scoped instructions.", nameof(userId));
            }
        }
        else if (userId is not null)
        {
            throw new ArgumentException("user_id must be null for system-scoped instructions.", nameof(userId));
        }
    }

    private static List<int> FindAllIndices(string haystack, string needle)
    {
        var result = new List<int>();
        var from = 0;
        while (true)
        {
            var idx = haystack.IndexOf(needle, from, StringComparison.Ordinal);
            if (idx < 0)
            {
                break;
            }

            result.Add(idx);
            from = idx + needle.Length;
            if (needle.Length == 0)
            {
                break;
            }
        }

        return result;
    }

    private static string BuildQueryPreview(string oldText)
    {
        const int max = 80;
        return oldText.Length <= max ? oldText : oldText[..max];
    }

    private static List<int> ToMatchLines(string text, List<int> indices, int limit)
    {
        var result = new List<int>(Math.Min(indices.Count, limit));
        for (var i = 0; i < indices.Count && result.Count < limit; i++)
        {
            result.Add(LineNumberAt(text, indices[i]));
        }

        return result;
    }

    private static int LineNumberAt(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
