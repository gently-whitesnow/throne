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
/// defaults. Anything off the curated whitelist raises a 400.
/// </summary>
public sealed class TerminalLaunchResolver(ITerminalSettingsStore settings)
{
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

        // Native default model = first entry of the vendor's curated list.
        var resolvedModel = model ?? TerminalAgentCatalog.ModelsFor(resolvedVendor)[0];
        if (!TerminalAgentCatalog.IsKnownModel(resolvedVendor, resolvedModel))
        {
            throw TerminalFailures.ModelInvalid(resolvedVendor, resolvedModel);
        }

        var resolvedEffort = effort ?? TerminalAgentCatalog.DefaultEffortFor(resolvedVendor);
        if (!TerminalAgentCatalog.IsKnownEffort(resolvedEffort))
        {
            throw TerminalFailures.EffortInvalid(resolvedEffort);
        }

        return new TerminalLaunchOptions(resolvedVendor, resolvedModel, resolvedEffort);
    }
}
