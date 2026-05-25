using Throne.Application.Git;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Default <see cref="IGitProviderRegistry"/>. Indexes the registered providers
/// by <see cref="IGitProvider.ProviderName"/> at construction time so lookups
/// are O(1). The registry handles the empty case so the host can boot and
/// surface a clear «no providers registered» message on the settings page.
/// </summary>
internal sealed class GitProviderRegistry : IGitProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IGitProvider> _byName;

    public GitProviderRegistry(IEnumerable<IGitProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var map = new Dictionary<string, IGitProvider>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            if (!map.TryAdd(provider.ProviderName, provider))
            {
                throw new InvalidOperationException(
                    $"Duplicate IGitProvider registration for '{provider.ProviderName}'.");
            }
        }

        _byName = map;
        AllProviders = map.Values.ToArray();
    }

    public IReadOnlyCollection<IGitProvider> AllProviders { get; }

    public IGitProvider? GetByName(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return _byName.GetValueOrDefault(providerName);
    }
}
