namespace Throne.Application.Search;

/// <summary>
/// One ranked search result: the matched intent id plus a snippet of the matching text
/// with the matched terms wrapped in <see cref="IntentSearchMarkers"/>. Ordering of a hit
/// list is by descending relevance (best match first) — callers must preserve it.
/// </summary>
public sealed record IntentSearchHit(string IntentId, string Snippet);

/// <summary>
/// Search-core read port: ranked full-text search over intents. This is the single engine
/// the link autocomplete consumes today and the future global search consumes tomorrow; it
/// is deliberately ignorant of any caller-specific filters (status / tag / pinned) and of
/// any UI. Callers layer their own structural filters on top of the returned ids.
/// </summary>
public interface IIntentSearchReader
{
    /// <param name="query">Raw user text. Tokenised and matched as prefix terms (AND).</param>
    /// <param name="limit">Maximum number of ranked hits to return.</param>
    /// <returns>
    /// Hits ordered best-match-first. Empty when <paramref name="query"/> carries no
    /// searchable token.
    /// </returns>
    Task<IReadOnlyList<IntentSearchHit>> SearchAsync(string query, int limit, CancellationToken ct);
}
