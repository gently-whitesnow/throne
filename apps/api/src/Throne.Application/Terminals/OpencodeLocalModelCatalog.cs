using Throne.Application.LocalModels;

namespace Throne.Application.Terminals;

/// <summary>
/// Bridges OpenCode (a <see cref="TerminalAgentCatalog.ModelSourceLocal"/> vendor) to the
/// shared local OpenAI-compatible discovery owned by
/// <see cref="LocalModelDiscoveryService"/>. Returning an empty list when the endpoint is
/// unconfigured, unreachable, or empty is part of the contract — both
/// <see cref="TerminalLaunchResolver"/> and the vendor catalog projection treat that as
/// "no models", surfaced to the operator instead of guessing a phantom default.
/// </summary>
public sealed class OpencodeLocalModelCatalog(LocalModelDiscoveryService discovery) : IVendorModelCatalog
{
    public string Vendor => TerminalAgentCatalog.VendorOpencode;

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct)
    {
        var result = await discovery.DiscoverAsync(ct);
        return result.Models;
    }
}
