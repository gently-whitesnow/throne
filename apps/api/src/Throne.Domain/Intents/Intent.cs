using Throne.Domain.Tags;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents;

public sealed class Intent
{
    private readonly List<TagId> _tagIds;

    private Intent(
        IntentId id,
        string text,
        string status,
        int currentVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Text = text;
        Status = status;
        CurrentVersion = currentVersion;
        _tagIds = [.. tagIds];
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public IntentId Id { get; }
    public string Text { get; private set; }
    public string Status { get; private set; }
    public int CurrentVersion { get; private set; }
    public IReadOnlyList<TagId> TagIds => _tagIds;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Intent Create(
        IntentId id,
        string text,
        IReadOnlyList<TagId>? tagIds,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            throw new ArgumentException("Intent text must not be empty.", nameof(text));
        }

        var normalized = NormalizeTagIds(tagIds);
        return new Intent(id, text, IntentStatusNames.Draft, currentVersion: 1, normalized, now, now);
    }

    public static Intent Restore(
        IntentId id,
        string text,
        string status,
        int currentVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ValidateStatus(status, nameof(status));
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }

        return new Intent(id, text, status, currentVersion, tagIds, createdAt, updatedAt);
    }

    public bool SetTagIds(IReadOnlyList<TagId> tagIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        var normalized = NormalizeTagIds(tagIds);
        if (TagIdListsEqual(_tagIds, normalized))
        {
            return false;
        }

        _tagIds.Clear();
        _tagIds.AddRange(normalized);
        UpdatedAt = now;
        return true;
    }

    public bool SetStatus(string status, DateTimeOffset now)
    {
        ValidateStatus(status, nameof(status));
        if (string.Equals(Status, status, StringComparison.Ordinal))
        {
            return false;
        }

        Status = status;
        UpdatedAt = now;
        return true;
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

    public InsertTextResult InsertAfterLine(
        int afterLine,
        string insertText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(insertText);
        ArgumentException.ThrowIfNullOrEmpty(newVersionId);

        var totalLines = Text.Length == 0 ? 0 : 1;
        for (var i = 0; i < Text.Length; i++)
        {
            if (Text[i] == '\n')
            {
                totalLines++;
            }
        }

        if (afterLine < 0 || afterLine > totalLines)
        {
            return new InsertTextResult.LineOutOfRange(totalLines, afterLine);
        }

        var insertIndex = afterLine == 0 ? 0 : FindLineEndOffset(Text, afterLine);
        Text = string.Concat(Text.AsSpan(0, insertIndex), insertText, Text.AsSpan(insertIndex));
        CurrentVersion += 1;
        UpdatedAt = now;

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Intent,
            OwnerId: Id.Value,
            Version: CurrentVersion,
            Kind: TextVersionKind.Insert,
            Snapshot: null,
            OldText: null,
            NewText: null,
            AfterLine: afterLine,
            InsertText: insertText,
            ChangedAt: now,
            ChangedBy: changedBy);

        return new InsertTextResult.Inserted(version);
    }

    public InsertTextResult AppendText(
        string insertText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        var totalLines = Text.Length == 0 ? 0 : 1;
        for (var i = 0; i < Text.Length; i++)
        {
            if (Text[i] == '\n')
            {
                totalLines++;
            }
        }

        return InsertAfterLine(totalLines, insertText, newVersionId, now, changedBy);
    }

    private static int FindLineEndOffset(string text, int line1Indexed)
    {
        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                seen++;
                if (seen == line1Indexed)
                {
                    return i + 1;
                }
            }
        }

        return text.Length;
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

    private static List<TagId> NormalizeTagIds(IReadOnlyList<TagId>? tagIds)
    {
        if (tagIds is null || tagIds.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TagId>(tagIds.Count);
        foreach (var id in tagIds)
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                continue;
            }

            if (seen.Add(id.Value))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static bool TagIdListsEqual(List<TagId> left, List<TagId> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Value, right[i].Value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateStatus(string status, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!IntentStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(paramName, $"Unknown intent status: {status}.");
        }
    }
}
