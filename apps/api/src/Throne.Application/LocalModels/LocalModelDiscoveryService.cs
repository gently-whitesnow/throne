using Throne.Application.Ports;

namespace Throne.Application.LocalModels;

/// <summary>
/// Resolves the configured local OpenAI-compatible endpoint into a discovery result, owning the
/// fallback policy so callers never branch on a missing setting or a thrown probe. Primary source
/// of truth for dynamic models is <c>GET {BaseUrl}/v1/models</c> — never parsing <c>opencode models</c>.
/// </summary>
public sealed class LocalModelDiscoveryService(LocalModelSettings settings, ILocalModelCatalogPort catalog)
{
    public async Task<LocalModelDiscoveryResult> DiscoverAsync(CancellationToken ct)
    {
        var baseUrl = settings.BaseUrl?.Trim();
        if (string.IsNullOrEmpty(baseUrl))
        {
            return LocalModelDiscoveryResult.NotConfigured();
        }

        try
        {
            var models = await catalog.ListModelIdsAsync(baseUrl, ct);
            return models.Count == 0
                ? LocalModelDiscoveryResult.EmptyCatalog(baseUrl)
                : LocalModelDiscoveryResult.Ready(baseUrl, models);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return LocalModelDiscoveryResult.Unreachable(baseUrl, ex.Message);
        }
    }
}
