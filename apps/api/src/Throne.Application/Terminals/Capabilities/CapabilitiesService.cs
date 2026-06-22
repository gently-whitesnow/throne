using Throne.Application.Errors;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Application orchestrator for the carrier-capability singleton. Combines the static
/// catalog (<see cref="CapabilityCatalog"/>), the persisted operator selection
/// (<see cref="CapabilitiesPersistence"/>) and the TTL-cached provider probes
/// (<see cref="ICapabilityDetectionCache"/>) into a single materialised view.
/// </summary>
public sealed class CapabilitiesService(
    CapabilitiesPersistence persistence,
    ICapabilityDetectionCache detection)
{
    public async Task<IReadOnlyList<CapabilityView>> ListAsync(CancellationToken ct)
    {
        var stored = await persistence.GetAsync(ct);
        var views = new List<CapabilityView>(CapabilityCatalog.Descriptors.Count);
        foreach (var descriptor in CapabilityCatalog.Descriptors)
        {
            views.Add(await BuildViewAsync(descriptor, stored, ct));
        }
        return views;
    }

    public async Task<CapabilityView> SetSelectedProviderAsync(
        string name,
        string? provider,
        CancellationToken ct)
    {
        var descriptor = CapabilityCatalog.FindByName(name)
            ?? throw CapabilityFailures.UnknownCapability(name);

        if (!string.IsNullOrWhiteSpace(provider)
            && descriptor.FindProvider(provider) is null)
        {
            throw CapabilityFailures.UnknownProvider(descriptor.Name, provider);
        }

        await persistence.UpsertSelectionAsync(descriptor.Name, provider, ct);
        var refreshed = await persistence.GetAsync(ct);
        return await BuildViewAsync(descriptor, refreshed, ct);
    }

    private async Task<CapabilityView> BuildViewAsync(
        CapabilityDescriptor descriptor,
        CapabilitiesAggregate? stored,
        CancellationToken ct)
    {
        var providerViews = new List<CapabilityProviderView>(descriptor.Providers.Count);
        foreach (var providerDescriptor in descriptor.Providers)
        {
            var probe = await detection.GetAsync(providerDescriptor.Name, ct);
            providerViews.Add(new CapabilityProviderView(
                Name: providerDescriptor.Name,
                Title: providerDescriptor.Title,
                PrerequisiteHint: providerDescriptor.PrerequisiteHint,
                Detected: probe?.Detected ?? false,
                DetectionDetail: probe?.Detail));
        }

        var selected = stored?.GetSelectedProvider(descriptor.Name);
        return new CapabilityView(
            Name: descriptor.Name,
            Title: descriptor.Title,
            Description: descriptor.Description,
            SelectedProvider: selected,
            Providers: providerViews);
    }
}

internal static class CapabilityFailures
{
    public static ApiException UnknownCapability(string name) =>
        new(
            ErrorCodes.CapabilityNotFound,
            $"Unknown capability '{name}'.",
            new Dictionary<string, object?> { ["capability"] = name });

    public static ApiException UnknownProvider(string capability, string provider) =>
        new(
            ErrorCodes.CapabilityProviderNotFound,
            $"Provider '{provider}' is not registered for capability '{capability}'.",
            new Dictionary<string, object?>
            {
                ["capability"] = capability,
                ["provider"] = provider,
            });
}
