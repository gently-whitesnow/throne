namespace Throne.Application.Terminals;

/// <summary>
/// Live model whitelist for vendors whose <see cref="TerminalVendorDescriptor.ModelSource"/> is
/// <see cref="TerminalAgentCatalog.ModelSourceLocal"/>. The static
/// <see cref="TerminalVendorDescriptor.Models"/> for such vendors is empty; the resolver and the
/// catalog projection call this seam to materialise the current list. Implementations live close
/// to their data source (OpenCode → local OpenAI-compatible discovery). Returning an empty list
/// means the endpoint is unconfigured or unreachable — surface it to the launch UI rather than
/// failing the catalog projection.
/// </summary>
public interface IVendorModelCatalog
{
    /// <summary>Vendor token (<see cref="TerminalAgentCatalog"/> constant) this catalog serves.</summary>
    string Vendor { get; }

    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct);
}
