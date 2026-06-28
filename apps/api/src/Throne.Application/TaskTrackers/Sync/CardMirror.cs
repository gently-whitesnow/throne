namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Direct field mapping between a tracker card and its mirror intent: <c>card.title ↔ intent.title</c>,
/// <c>card.description ↔ intent.text</c>. Replaces the old single-text compose/decompose hack.
/// </summary>
public static class CardMirror
{
    /// <summary>
    /// The intent text mirrors the card description. An empty description has no text to mirror, so the
    /// title seeds the (always non-empty) intent text — preserving the title-only-card behaviour from
    /// before title and text were split into separate fields.
    /// </summary>
    public static string TextOf(string title, string? description) =>
        string.IsNullOrWhiteSpace(description) ? title : description;
}
