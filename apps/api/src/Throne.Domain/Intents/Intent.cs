using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents;

public sealed class Intent
{
    private readonly List<string> _tags;

    private Intent(
        IntentId id,
        string text,
        int currentVersion,
        IReadOnlyList<string> tags,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Text = text;
        CurrentVersion = currentVersion;
        _tags = [.. tags];
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public IntentId Id { get; }
    public string Text { get; private set; }
    public int CurrentVersion { get; private set; }
    public IReadOnlyList<string> Tags => _tags;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Intent Create(
        IntentId id,
        string text,
        IReadOnlyList<string>? tags,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            throw new ArgumentException("Intent text must not be empty.", nameof(text));
        }

        var normalizedTags = NormalizeTags(tags);
        return new Intent(id, text, currentVersion: 1, normalizedTags, now, now);
    }

    public static Intent Restore(
        IntentId id,
        string text,
        int currentVersion,
        IReadOnlyList<string> tags,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        return new Intent(id, text, currentVersion, tags, createdAt, updatedAt);
    }

    public ReplaceTextResult ReplaceText(
        string oldText,
        string newText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentException.ThrowIfNullOrEmpty(newVersionId);
        if (oldText.Length == 0)
        {
            throw new ArgumentException("old_text must not be empty.", nameof(oldText));
        }

        var indices = FindAllIndices(Text, oldText);
        if (indices.Count == 0)
        {
            return new ReplaceTextResult.MatchNotFound(BuildQueryPreview(oldText));
        }

        if (indices.Count > 1)
        {
            return new ReplaceTextResult.MatchAmbiguous(
                indices.Count,
                ToMatchLines(Text, indices, limit: 5));
        }

        var index = indices[0];
        Text = string.Concat(Text.AsSpan(0, index), newText, Text.AsSpan(index + oldText.Length));
        CurrentVersion += 1;
        UpdatedAt = now;

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Intent,
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

        return new ReplaceTextResult.Replaced(version);
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

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null || tags.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(tags.Count);
        foreach (var raw in tags)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }
}
