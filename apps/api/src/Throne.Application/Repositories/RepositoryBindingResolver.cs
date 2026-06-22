using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed class RepositoryBindingResolver(
    IIntentRepository intents,
    IIntentRepositoryBindingRepository bindings,
    IGitProviderRegistry providers)
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

    public static async Task EnsureProviderAuthenticatedAsync(IGitProvider provider, CancellationToken ct)
    {
        var status = await provider.GetAuthStatusAsync(ct);
        if (!status.IsAuthenticated)
        {
            throw RepositoryBindingFailures.ProviderNotAuthenticated(status);
        }
    }
}
