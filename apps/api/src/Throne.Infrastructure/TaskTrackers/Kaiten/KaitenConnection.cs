namespace Throne.Infrastructure.TaskTrackers.Kaiten;

/// <summary>
/// Per-request connection to one Kaiten company: the workspace base URL (e.g.
/// <c>https://mycompany.kaiten.ru</c>) and the bearer token. The adapter is stateless with respect
/// to credentials — they are passed on every call rather than captured at construction, so a single
/// registered client serves every connection and never holds a secret in a field. The token comes
/// from the encrypted connection-settings axis (out of scope here); this slice only consumes it.
/// </summary>
internal sealed record KaitenConnection(string BaseUrl, string Token)
{
    /// <summary>
    /// REST root for v1 endpoints. Kaiten serves the stable surface under <c>/api/v1</c> (the card
    /// CRUD/comments/tags/children paths all hang off it); the trailing slash on the workspace URL is
    /// normalised away so callers may pass it either way.
    /// </summary>
    public string ApiBaseUrl => $"{BaseUrl.TrimEnd('/')}/api/v1";
}
