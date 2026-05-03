namespace Throne.Application.DreamRuns;

/// <summary>
/// Inserts a one-line rule into the <c>## Learned rules</c> section of an instruction,
/// creating the section if absent. The transformation is deterministic so apply preview
/// and apply itself produce identical output.
/// </summary>
public static class LearnedRulesInjector
{
    public const string SectionHeader = "## Learned rules";

    public static string Inject(string currentText, string rule)
    {
        ArgumentNullException.ThrowIfNull(currentText);
        ArgumentException.ThrowIfNullOrWhiteSpace(rule);
        var bullet = "- " + rule.Trim();

        if (currentText.Length == 0)
        {
            return $"{SectionHeader}\n\n{bullet}\n";
        }

        var headerIndex = currentText.IndexOf(SectionHeader, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            var trailing = currentText.EndsWith('\n') ? currentText : currentText + "\n";
            return $"{trailing}\n{SectionHeader}\n\n{bullet}\n";
        }

        var afterHeader = currentText.IndexOf('\n', headerIndex);
        if (afterHeader < 0)
        {
            // Section header is the very last line — append blank line + bullet.
            return $"{currentText}\n\n{bullet}\n";
        }

        // Insert bullet right after a single blank line under the header. We don't try
        // to reorder existing bullets — the new rule lands at the top of the section.
        var insertAt = afterHeader + 1;
        var blankLineExists = insertAt < currentText.Length && currentText[insertAt] == '\n';
        var prefix = currentText[..insertAt];
        var suffix = currentText[insertAt..];
        if (blankLineExists)
        {
            return $"{prefix}\n{bullet}\n{suffix[1..]}";
        }
        return $"{prefix}\n{bullet}\n{suffix}";
    }
}
