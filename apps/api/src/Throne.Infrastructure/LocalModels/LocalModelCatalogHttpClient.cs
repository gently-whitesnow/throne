using Throne.Application.Ports;

namespace Throne.Infrastructure.LocalModels;

/// <summary>
/// Reads the model catalog from a local OpenAI-compatible endpoint via
/// <c>GET {baseUrl}/v1/models</c>. Uses <see cref="IHttpClientFactory"/> so handler lifetime is
/// pooled even though the consuming services are singletons. Any non-success status or unparseable
/// body throws — <see cref="Throne.Application.LocalModels.LocalModelDiscoveryService"/> turns that
/// into the controlled <c>unreachable</c> state.
/// </summary>
internal sealed class LocalModelCatalogHttpClient(IHttpClientFactory httpClientFactory) : ILocalModelCatalogPort
{
    public const string HttpClientName = "local-model-catalog";

    public async Task<IReadOnlyList<string>> ListModelIdsAsync(string baseUrl, CancellationToken ct)
    {
        var requestUri = $"{baseUrl.TrimEnd('/')}/v1/models";
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(ct);
        return OpenAiModelsNormalizer.Normalize(payload);
    }
}
