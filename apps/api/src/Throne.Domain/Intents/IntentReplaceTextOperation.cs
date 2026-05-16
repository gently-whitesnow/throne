using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents;

public static class IntentReplaceTextOperation
{
    public static ReplaceTextResult Apply(
        Intent intent,
        string oldText,
        string newText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentException.ThrowIfNullOrEmpty(newVersionId);
        if (oldText.Length == 0)
        {
            throw new ArgumentException("old_text must not be empty.", nameof(oldText));
        }

        var current = intent.State.Text;
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
        var newVersion = intent.State.CurrentVersion + 1;
        intent.State = intent.State with
        {
            Text = updatedText,
            CurrentVersion = newVersion,
            UpdatedAt = now,
        };

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Intent,
            OwnerId: intent.Id.Value,
            Version: newVersion,
            Kind: TextVersionKind.Replace,
            Delta: new TextVersionDelta(null, oldText, newText, null, null),
            ChangedAt: now,
            ChangedBy: changedBy);

        return new ReplaceTextResult.Replaced(version);
    }
}
