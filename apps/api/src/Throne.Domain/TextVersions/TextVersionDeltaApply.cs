namespace Throne.Domain.TextVersions;

/// <summary>
/// Pure delta application used by <see cref="TextVersionReplay"/> to fold
/// individual <see cref="TextVersion"/> entries onto a running text. Lives in
/// its own class so the per-type cyclomatic budget of
/// <see cref="TextVersionReplay"/> stays trivially under threshold — the
/// split is structural, not behavioural.
/// </summary>
internal static class TextVersionDeltaApply
{
    public static string To(string text, TextVersion version) => version.Kind switch
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
        // the next replace populates it (see InstructionGuards.
        // EnsureValidOldTextForReplace). string.IndexOf with an empty needle
        // returns 0 by spec, so this branch correctly produces
        // `new_text + text` (effectively a prepend / set-from-empty).
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
