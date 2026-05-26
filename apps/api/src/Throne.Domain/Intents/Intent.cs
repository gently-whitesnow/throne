using Throne.Domain.Tags;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents;

/// <summary>
/// Aggregate root for a user-owned intent — free-form text plus lifecycle status,
/// tag membership and a fractional-index sort key. The mutable portion is grouped
/// into the <see cref="IntentState"/> record so transitions are written as
/// <c>State = State with { … }</c>.
///
/// Per ADR-0025 the aggregate is rich-DDD: invariants and state transitions live
/// inside this class as <c>Create</c>/<c>Restore</c> factories and instance mutator
/// methods. No external <c>*Factory</c> / <c>*Guards</c> / <c>*Operation</c>
/// satellites are used.
///
/// State machine notes:
/// <list type="bullet">
///   <item><see cref="MoveTo"/> changes only <see cref="IntentState.SortKey"/> —
///         it must not bump <see cref="IntentState.CurrentVersion"/> nor
///         <see cref="IntentState.UpdatedAt"/>; drag-and-drop is purely positional
///         and stays out of text-edit history.</item>
///   <item><see cref="SetTags"/> bumps <see cref="IntentState.UpdatedAt"/> but not
///         <see cref="IntentState.CurrentVersion"/> — tag membership is metadata,
///         not part of versioned text.</item>
///   <item>Text-mutating transitions (<see cref="ReplaceText"/>,
///         <see cref="InsertTextAfterLine"/>, <see cref="AppendText"/>) bump both
///         <see cref="IntentState.CurrentVersion"/> and <see cref="IntentState.UpdatedAt"/>
///         and emit a <see cref="TextVersion"/> describing the delta.</item>
/// </list>
/// </summary>
public sealed class Intent
{
    private readonly List<TagId> _tagIds;

    private Intent(
        IntentId id,
        string ownerUserId,
        IntentState state,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        State = state;
        _tagIds = [.. tagIds];
        CreatedAt = createdAt;
    }

    public IntentId Id { get; }
    public string OwnerUserId { get; }
    public DateTimeOffset CreatedAt { get; }
    public IntentState State { get; private set; }
    public IReadOnlyList<TagId> TagIds => _tagIds;

    public static Intent Create(
        IntentId id,
        string ownerUserId,
        string text,
        IReadOnlyList<TagId>? tagIds,
        DateTimeOffset now,
        string? sortKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            throw new ArgumentException("Intent text must not be empty.", nameof(text));
        }
        var resolvedSortKey = sortKey ?? FractionalIndex.Initial();
        FractionalIndex.ValidateKey(resolvedSortKey, nameof(sortKey));
        var normalized = NormalizeTagIds(tagIds);
        var state = new IntentState(text, IntentStatusNames.Draft, CurrentVersion: 1, resolvedSortKey, now);
        return new Intent(id, ownerUserId, state, normalized, now);
    }

    public static Intent Restore(
        IntentId id,
        string ownerUserId,
        string text,
        string status,
        int currentVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? sortKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        EnsureValidStatus(status, nameof(status));
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }
        var resolvedSortKey = sortKey ?? FractionalIndex.Initial();
        FractionalIndex.ValidateKey(resolvedSortKey, nameof(sortKey));
        var state = new IntentState(text, status, currentVersion, resolvedSortKey, updatedAt);
        return new Intent(id, ownerUserId, state, tagIds, createdAt);
    }

    /// <summary>
    /// Status-machine transition. Idempotent — returns <c>false</c> if the status
    /// already matches. Does not bump <see cref="IntentState.CurrentVersion"/>.
    /// </summary>
    public bool SetStatus(string status, DateTimeOffset now)
    {
        EnsureValidStatus(status, nameof(status));
        if (string.Equals(State.Status, status, StringComparison.Ordinal))
        {
            return false;
        }

        State = State with { Status = status, UpdatedAt = now };
        return true;
    }

    /// <summary>
    /// Reorder by assigning a new sort key. Idempotent — returns <c>false</c> if
    /// the key already matches. Does not bump version nor UpdatedAt; positional
    /// changes must not pollute text-edit history.
    /// </summary>
    public bool MoveTo(string newSortKey)
    {
        FractionalIndex.ValidateKey(newSortKey, nameof(newSortKey));
        if (string.Equals(State.SortKey, newSortKey, StringComparison.Ordinal))
        {
            return false;
        }

        State = State with { SortKey = newSortKey };
        return true;
    }

    /// <summary>
    /// Replace tag membership with the given set, deduplicated and order-preserved.
    /// Bumps <see cref="IntentState.UpdatedAt"/> only when the normalised set
    /// differs from the current one. Does not bump <see cref="IntentState.CurrentVersion"/>.
    /// </summary>
    public bool SetTags(IReadOnlyList<TagId> tagIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tagIds);
        var normalized = NormalizeTagIds(tagIds);
        if (TagIdSequenceEqual(_tagIds, normalized))
        {
            return false;
        }

        _tagIds.Clear();
        _tagIds.AddRange(normalized);
        State = State with { UpdatedAt = now };
        return true;
    }

    /// <summary>
    /// Replace the single unique occurrence of <paramref name="oldText"/> with
    /// <paramref name="newText"/>. Returns <see cref="ReplaceTextResult.MatchNotFound"/>
    /// or <see cref="ReplaceTextResult.MatchAmbiguous"/> without mutating state.
    /// </summary>
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

        var current = State.Text;
        var indices = TextEditMatcher.FindAllIndices(current, oldText);
        if (indices.Count == 0)
        {
            return new ReplaceTextResult.MatchNotFound(TextEditMatcher.BuildQueryPreview(oldText));
        }

        if (indices.Count > 1)
        {
            return new ReplaceTextResult.MatchAmbiguous(
                indices.Count,
                TextEditLineLookup.ToMatchLines(current, indices, limit: 5));
        }

        var index = indices[0];
        var updatedText = string.Concat(current.AsSpan(0, index), newText, current.AsSpan(index + oldText.Length));
        var newVersion = State.CurrentVersion + 1;
        State = State with
        {
            Text = updatedText,
            CurrentVersion = newVersion,
            UpdatedAt = now,
        };

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Intent,
            OwnerId: Id.Value,
            Version: newVersion,
            Kind: TextVersionKind.Replace,
            Delta: new TextVersionDelta(null, oldText, newText, null, null),
            ChangedAt: now,
            ChangedBy: changedBy);

        return new ReplaceTextResult.Replaced(version);
    }

    /// <summary>
    /// Insert <paramref name="insertText"/> after line <paramref name="afterLine"/>
    /// (1-based; <c>0</c> means «at the very top»). Returns
    /// <see cref="InsertTextResult.LineOutOfRange"/> without mutating state.
    /// </summary>
    public InsertTextResult InsertTextAfterLine(
        int afterLine,
        string insertText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(insertText);
        ArgumentException.ThrowIfNullOrEmpty(newVersionId);

        var current = State.Text;
        var totalLines = TextEditLineCount.CountLines(current);
        if (afterLine < 0 || afterLine > totalLines)
        {
            return new InsertTextResult.LineOutOfRange(totalLines, afterLine);
        }

        var insertIndex = afterLine == 0 ? 0 : TextEditLineCount.FindLineEndOffset(current, afterLine);
        var updatedText = string.Concat(current.AsSpan(0, insertIndex), insertText, current.AsSpan(insertIndex));
        var newVersion = State.CurrentVersion + 1;
        State = State with
        {
            Text = updatedText,
            CurrentVersion = newVersion,
            UpdatedAt = now,
        };

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Intent,
            OwnerId: Id.Value,
            Version: newVersion,
            Kind: TextVersionKind.Insert,
            Delta: new TextVersionDelta(null, null, null, afterLine, insertText),
            ChangedAt: now,
            ChangedBy: changedBy);

        return new InsertTextResult.Inserted(version);
    }

    /// <summary>
    /// Convenience wrapper over <see cref="InsertTextAfterLine"/> that inserts at
    /// the end of the current text.
    /// </summary>
    public InsertTextResult AppendText(
        string insertText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        var totalLines = TextEditLineCount.CountLines(State.Text);
        return InsertTextAfterLine(totalLines, insertText, newVersionId, now, changedBy);
    }

    private static void EnsureValidStatus(string status, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!IntentStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(paramName, $"Unknown intent status: {status}.");
        }
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

    private static bool TagIdSequenceEqual(List<TagId> left, List<TagId> right)
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
}
