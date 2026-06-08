using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed class RepositoryBindingResolver(
    IIntentRepository intents,
    IIntentRepositoryBindingRepository bindings,
    IGitProviderRegistry providers,
    CapabilitiesPersistence? capabilities = null)
{
    public async Task<IntentId> EnsureIntentExistsAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var id = new IntentId(intentId);
        var intent = await intents.GetByIdAsync(id, ct)
            ?? throw RepositoryBindingFailures.IntentNotFound(intentId);
        return intent.Id;
    }

    public async Task<IntentRepositoryBinding> LoadBindingAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingId);

        var binding = await bindings.GetByIdAsync(new BindingId(bindingId), ct)
            ?? throw RepositoryBindingFailures.BindingNotFound(intentId, bindingId);

        if (binding.IntentId.Value != intentId)
        {
            throw RepositoryBindingFailures.BindingNotFound(intentId, bindingId);
        }

        return binding;
    }

    public IGitProvider ResolveProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        return providers.GetByName(providerName)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(providerName);
    }

    public async Task EnsureProviderSurfaceEnabledAsync(string providerName, CancellationToken ct)
    {
        if (providerName != GitProviderNames.GitLab)
        {
            return;
        }

        var stored = capabilities is null ? null : await capabilities.GetAsync(ct);
        if (stored is null || !stored.IsEnabled(CapabilityNames.Repositories))
        {
            throw RepositoryBindingFailures.ProviderCapabilityDisabled(providerName, CapabilityNames.Repositories);
        }
        if (!stored.IsEnabled(CapabilityNames.Gitlab))
        {
            throw RepositoryBindingFailures.ProviderCapabilityDisabled(providerName, CapabilityNames.Gitlab);
        }
    }

    public static async Task EnsureProviderAuthenticatedAsync(IGitProvider provider, CancellationToken ct)
    {
        var status = await provider.GetAuthStatusAsync(ct);
        if (!status.IsAuthenticated)
        {
            throw RepositoryBindingFailures.ProviderNotAuthenticated(status);
        }
    }
}
