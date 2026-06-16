using Throne.Application.Ports;

namespace Throne.Application.Terminals;

/// <summary>
/// Raw, wire-level launch axis straight from the request DTO — every field optional.
/// Resolved into a defaulted <see cref="TerminalLaunchOptions"/> by
/// <see cref="TerminalLaunchResolver"/>.
/// </summary>
public sealed record TerminalLaunchInput(string? Vendor, string? Model, string? Effort);

/// <summary>
/// Resolves the optional, wire-level launch axis (vendor / model / effort) into a fully
/// defaulted, whitelist-checked <see cref="TerminalLaunchOptions"/>. Omitted vendor falls
/// back to the persisted setting; omitted model/effort fall back to the vendor's native
/// defaults. Anything off the curated whitelist raises a 400. A vendor whose descriptor
/// declares no effort axis resolves to a null effort — any effort the caller passed is
/// dropped and no effort flag reaches the spawn argv.
///
/// For vendors whose <see cref="TerminalVendorDescriptor.ModelSource"/> is
/// <see cref="TerminalAgentCatalog.ModelSourceLocal"/> the static
/// <see cref="TerminalVendorDescriptor.Models"/> is empty by design: the live list is
/// fetched through the matching <see cref="IVendorModelCatalog"/>. An unconfigured or
/// unreachable local endpoint surfaces as an empty list — the resolver fails the launch
/// with the same args-invalid code rather than picking a phantom default.
/// </summary>
public sealed class TerminalLaunchResolver(
    ITerminalSettingsStore settings,
    IEnumerable<IVendorModelCatalog> dynamicCatalogs)
{
    private readonly Dictionary<string, IVendorModelCatalog> _dynamicCatalogs =
        dynamicCatalogs.ToDictionary(c => c.Vendor, StringComparer.Ordinal);

    public async Task<TerminalLaunchOptions> ResolveAsync(
        string? vendor,
        string? model,
        string? effort,
        CancellationToken ct)
    {
        var resolvedVendor = vendor ?? await settings.GetDefaultVendorAsync(ct);
        if (!TerminalAgentCatalog.IsKnownVendor(resolvedVendor))
        {
            throw TerminalFailures.VendorInvalid(resolvedVendor);
        }

        var descriptor = TerminalAgentCatalog.DescriptorFor(resolvedVendor);

        var resolvedModel = await ResolveModelAsync(descriptor, model, ct);

        string? resolvedEffort = null;
        if (descriptor.SupportsEffort)
        {
            resolvedEffort = effort ?? descriptor.DefaultEffort;
            if (resolvedEffort is null || !TerminalAgentCatalog.IsKnownEffort(resolvedEffort))
            {
                throw TerminalFailures.EffortInvalid(resolvedEffort ?? "(none)");
            }
        }

        return new TerminalLaunchOptions(resolvedVendor, resolvedModel, resolvedEffort);
    }

    private Task<string> ResolveModelAsync(
        TerminalVendorDescriptor descriptor,
        string? requestedModel,
        CancellationToken ct) =>
        descriptor.ModelSource == TerminalAgentCatalog.ModelSourceLocal
            ? ResolveLocalModelAsync(descriptor, requestedModel, ct)
            : Task.FromResult(ResolveStaticModel(descriptor, requestedModel));

    private async Task<string> ResolveLocalModelAsync(
        TerminalVendorDescriptor descriptor, string? requestedModel, CancellationToken ct)
    {
        if (!_dynamicCatalogs.TryGetValue(descriptor.Vendor, out var catalog))
        {
            throw TerminalFailures.ModelInvalid(descriptor.Vendor, requestedModel ?? "(none)");
        }

        var liveModels = await catalog.ListModelsAsync(ct);
        var resolved = requestedModel ?? (liveModels.Count == 0 ? null : liveModels[0]);
        if (resolved is null || !liveModels.Contains(resolved, StringComparer.Ordinal))
        {
            throw TerminalFailures.ModelInvalid(descriptor.Vendor, requestedModel ?? "(none)");
        }
        return resolved;
    }

    private static string ResolveStaticModel(TerminalVendorDescriptor descriptor, string? requestedModel)
    {
        var resolved = requestedModel ?? descriptor.DefaultModel;
        if (resolved is null || !descriptor.HasModel(resolved))
        {
            throw TerminalFailures.ModelInvalid(descriptor.Vendor, requestedModel ?? "(none)");
        }
        return resolved;
    }
}
