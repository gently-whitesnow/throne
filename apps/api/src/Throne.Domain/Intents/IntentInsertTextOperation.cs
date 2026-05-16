using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents;

public static class IntentInsertTextOperation
{
    public static InsertTextResult AfterLine(
        Intent intent,
        int afterLine,
        string insertText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(insertText);
        ArgumentException.ThrowIfNullOrEmpty(newVersionId);

        var current = intent.State.Text;
        var totalLines = TextEditLineCount.CountLines(current);
        if (afterLine < 0 || afterLine > totalLines)
        {
            return new InsertTextResult.LineOutOfRange(totalLines, afterLine);
        }

        var insertIndex = afterLine == 0 ? 0 : TextEditLineCount.FindLineEndOffset(current, afterLine);
        var updatedText = string.Concat(current.AsSpan(0, insertIndex), insertText, current.AsSpan(insertIndex));
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
            Kind: TextVersionKind.Insert,
            Delta: new TextVersionDelta(null, null, null, afterLine, insertText),
            ChangedAt: now,
            ChangedBy: changedBy);

        return new InsertTextResult.Inserted(version);
    }

    public static InsertTextResult Append(
        Intent intent,
        string insertText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var totalLines = TextEditLineCount.CountLines(intent.State.Text);
        return AfterLine(intent, totalLines, insertText, newVersionId, now, changedBy);
    }
}
