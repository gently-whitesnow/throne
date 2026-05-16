using Throne.Domain.TextVersions;

namespace Throne.Domain.Instructions;

public static class InstructionReplaceTextOperation
{
    public static ReplaceInstructionTextResult Apply(
        Instruction instruction,
        string oldText,
        string newText,
        string newVersionId,
        DateTimeOffset now,
        TextVersionAuthor changedBy)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);
        ArgumentException.ThrowIfNullOrEmpty(newVersionId);
        InstructionGuards.EnsureValidOldTextForReplace(oldText, instruction.Text);

        var indices = TextEditMatcher.FindAllIndices(instruction.Text, oldText);
        if (indices.Count == 0)
        {
            return new ReplaceInstructionTextResult.MatchNotFound(TextEditMatcher.BuildQueryPreview(oldText));
        }
        if (indices.Count > 1)
        {
            return new ReplaceInstructionTextResult.MatchAmbiguous(
                indices.Count,
                TextEditLineLookup.ToMatchLines(instruction.Text, indices, limit: 5));
        }

        var index = indices[0];
        var updatedText = string.Concat(
            instruction.Text.AsSpan(0, index),
            newText,
            instruction.Text.AsSpan(index + oldText.Length));
        instruction.Text = updatedText;
        instruction.CurrentVersion += 1;
        instruction.UpdatedAt = now;

        var version = new TextVersion(
            Id: newVersionId,
            OwnerKind: TextVersionOwnerKind.Instruction,
            OwnerId: instruction.Id.Value,
            Version: instruction.CurrentVersion,
            Kind: TextVersionKind.Replace,
            Delta: new TextVersionDelta(null, oldText, newText, null, null),
            ChangedAt: now,
            ChangedBy: changedBy);

        return new ReplaceInstructionTextResult.Replaced(version);
    }
}
