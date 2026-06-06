namespace Throne.Domain.TextVersions;

/// <summary>
/// Replays a stream of <see cref="TextVersion"/> deltas to reconstruct the
/// text of an owner (Instruction / Intent) at an arbitrary historical version.
///
/// Used by read-only views that need to compare a current state against a
/// historical baseline — e.g. patch diff preview which compares
/// <c>InstructionPatch.PatchText</c> against the instruction text at
/// <c>base_instruction_version</c>.
///
/// The replay is pure: it operates on the same delta semantics that
/// <c>Instruction.ReplaceText</c>, <c>Intent.ReplaceText</c> and
/// <c>Intent.InsertTextAfterLine</c> use when writing the history, so a
/// well-formed history reconstructs verbatim.
/// </summary>
public static class TextVersionReplay
{
    /// <summary>
    /// Replay all versions up to and including <paramref name="targetVersion"/>.
    /// </summary>
    /// <param name="versions">
    /// All versions for one owner. Order is irrelevant — they are sorted by
    /// <see cref="TextVersion.Version"/> internally. Versions above
    /// <paramref name="targetVersion"/> are ignored.
    /// </param>
    /// <param name="targetVersion">
    /// Version number to reconstruct. Returns <see cref="string.Empty"/> when
    /// nothing in <paramref name="versions"/> reaches it
    /// (e.g. <c>0</c> meaning «before history»).
    /// </param>
    /// <returns>The reconstructed text, or empty string if not reconstructible.</returns>
    public static string ReplayTo(IReadOnlyList<TextVersion> versions, int targetVersion)
    {
        ArgumentNullException.ThrowIfNull(versions);
        var ordered = versions
            .Where(v => v.Version > 0 && v.Version <= targetVersion)
            .OrderBy(v => v.Version);
        var text = string.Empty;
        foreach (var version in ordered)
        {
            text = ApplyDelta(text, version);
        }
        return text;
    }

    private static string ApplyDelta(string text, TextVersion version) => version.Kind switch
    {
        TextVersionKind.Create => version.Delta.Snapshot ?? string.Empty,
        TextVersionKind.Replace => Replace(text, version.Delta.OldText, version.Delta.NewText),
        TextVersionKind.Insert => Insert(text, version.Delta.AfterLine ?? 0, version.Delta.InsertText),
        _ => text,
    };

    private static string Replace(string text, string? oldText, string? newText)
    {
        // Empty `old_text` is a legitimate "initial fill" — it is what the
        // domain writes when the instruction was created with empty text and
        // the next replace populates it (see Instruction.ReplaceText guard
        // against empty old_text on non-empty current text). string.IndexOf
        // with an empty needle returns 0 by spec, so this branch correctly
        // produces `new_text + text` (effectively a prepend / set-from-empty).
        var needle = oldText ?? string.Empty;
        var index = text.IndexOf(needle, StringComparison.Ordinal);
        return index < 0
            ? text
            : string.Concat(
                text.AsSpan(0, index),
                newText ?? string.Empty,
                text.AsSpan(index + needle.Length));
    }

    private static string Insert(string text, int afterLine, string? insertText)
    {
        var insertIndex = afterLine <= 0
            ? 0
            : TextEditLineCount.FindLineEndOffset(text, afterLine);
        return string.Concat(
            text.AsSpan(0, insertIndex),
            insertText ?? string.Empty,
            text.AsSpan(insertIndex));
    }
}
