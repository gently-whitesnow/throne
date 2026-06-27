using Throne.Domain.TaskTrackers;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Bridges a card's title/description and the intent's single free-form text: the first line is the
/// title, the remainder (after one blank line) is the description. Echo suppression hinges on
/// <see cref="Matches"/> — the side that just wrote a value sees the mirror already equal to it and
/// skips, so a change never bounces back across the boundary.
/// </summary>
public static class CardTextComposer
{
    public static string Compose(string title, string? description)
    {
        var head = (title ?? string.Empty).TrimEnd();
        if (head.Length == 0)
        {
            head = "(без названия)";
        }

        return string.IsNullOrWhiteSpace(description)
            ? head
            : head + "\n\n" + description!.Trim();
    }

    public static (string Title, string? Description) Decompose(string text)
    {
        var value = text ?? string.Empty;
        var newline = value.IndexOf('\n');
        if (newline < 0)
        {
            return (value.Trim(), null);
        }

        var title = value[..newline].Trim();
        var rest = value[(newline + 1)..].Trim('\n', '\r');
        return (title, rest.Length == 0 ? null : rest);
    }

    /// <summary>True when the live intent text already equals the snapshot — i.e. nothing to push/pull.</summary>
    public static bool Matches(CardSyncLinkSnapshot snapshot, string intentText) =>
        string.Equals(Compose(snapshot.Title, snapshot.Description), intentText ?? string.Empty, StringComparison.Ordinal);
}
