namespace Throne.Application.Ports;

/// <summary>
/// Outbound port that reads the model catalog from a local OpenAI-compatible endpoint
/// (<c>GET {baseUrl}/v1/models</c>) and normalizes the response into model ids.
/// Transport, HTTP-status and parse failures surface as exceptions — the discovery
/// service translates them into the <c>unreachable</c> fallback state.
/// </summary>
public interface ILocalModelCatalogPort
{
    /// <summary>
    /// Returns the normalized model ids exposed by <paramref name="baseUrl"/>. The list may be
    /// empty when the endpoint is reachable but advertises no models. Throws on any transport,
    /// non-success status or unparseable payload.
    /// </summary>
    Task<IReadOnlyList<string>> ListModelIdsAsync(string baseUrl, CancellationToken ct);
}
