using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

/// <summary>
/// Projects <see cref="TerminalAgentCatalog"/> into the wire DTO for
/// <c>GET /api/v1/terminal/vendors</c>. Backend stays the single source of truth for vendor
/// metadata; the frontend reads this instead of mirroring the catalog. For descriptors with
/// <see cref="TerminalAgentCatalog.ModelSourceLocal"/> the static
/// <see cref="TerminalVendorDescriptor.Models"/> is empty and the live list is fetched via the
/// matching <see cref="IVendorModelCatalog"/>; an empty live list (endpoint unconfigured or
/// unreachable) projects to an empty <c>models</c> + null <c>default_model</c> so the UI can
/// still show the vendor and surface the missing-endpoint hint.
/// </summary>
public sealed class TerminalVendorCatalogMapper(IEnumerable<IVendorModelCatalog> dynamicCatalogs)
{
    private readonly Dictionary<string, IVendorModelCatalog> _dynamicCatalogs =
        dynamicCatalogs.ToDictionary(c => c.Vendor, StringComparer.Ordinal);

    public async Task<TerminalVendorCatalogResponse> ToDtoAsync(CancellationToken ct)
    {
        var vendors = new List<TerminalVendorMetadataDto>(TerminalAgentCatalog.Descriptors.Count);
        foreach (var descriptor in TerminalAgentCatalog.Descriptors)
        {
            vendors.Add(await ToVendorDtoAsync(descriptor, ct));
        }
        return new TerminalVendorCatalogResponse
        {
            Default_vendor = ParseVendor(TerminalAgentCatalog.DefaultVendor),
            Vendors = vendors,
        };
    }

    private async Task<TerminalVendorMetadataDto> ToVendorDtoAsync(
        TerminalVendorDescriptor descriptor, CancellationToken ct)
    {
        var models = await ResolveModelsAsync(descriptor, ct);
        return new TerminalVendorMetadataDto
        {
            Vendor = ParseVendor(descriptor.Vendor),
            Label = descriptor.Label,
            Supports_effort = descriptor.SupportsEffort,
            Models = models,
            Default_model = models.Count == 0 ? null : models[0],
            // Efforts wire-format is string[] (см. PR #97 / ADR-обоснование):
            // NSwag не цепляет JsonStringEnumConverter к коллекции enum'ов, поэтому
            // массив проходит сырыми строками из descriptor.Efforts. Default_effort
            // остаётся скалярным enum'ом — на нём конвертер работает.
            Efforts = descriptor.Efforts.ToArray(),
            Default_effort = descriptor.DefaultEffort is { } effort ? ParseEffort(effort) : null,
            Model_source = ParseModelSource(descriptor.ModelSource),
        };
    }

    private async Task<IList<string>> ResolveModelsAsync(
        TerminalVendorDescriptor descriptor, CancellationToken ct)
    {
        if (descriptor.ModelSource == TerminalAgentCatalog.ModelSourceLocal
            && _dynamicCatalogs.TryGetValue(descriptor.Vendor, out var dynamicCatalog))
        {
            var live = await dynamicCatalog.ListModelsAsync(ct);
            return live.ToArray();
        }
        return descriptor.Models.ToArray();
    }

    private static TerminalAgentVendor ParseVendor(string vendor) => vendor switch
    {
        TerminalAgentCatalog.VendorClaude => TerminalAgentVendor.Claude,
        TerminalAgentCatalog.VendorCodex => TerminalAgentVendor.Codex,
        TerminalAgentCatalog.VendorOpencode => TerminalAgentVendor.Opencode,
        _ => throw new InvalidOperationException($"Unknown terminal vendor '{vendor}'."),
    };

    private static TerminalReasoningEffort ParseEffort(string effort) => effort switch
    {
        TerminalAgentCatalog.EffortLow => TerminalReasoningEffort.Low,
        TerminalAgentCatalog.EffortMedium => TerminalReasoningEffort.Medium,
        TerminalAgentCatalog.EffortHigh => TerminalReasoningEffort.High,
        TerminalAgentCatalog.EffortXhigh => TerminalReasoningEffort.Xhigh,
        _ => throw new InvalidOperationException($"Unknown reasoning effort '{effort}'."),
    };

    private static TerminalModelSource ParseModelSource(string source) => source switch
    {
        TerminalAgentCatalog.ModelSourceStatic => TerminalModelSource.Static,
        TerminalAgentCatalog.ModelSourceLocal => TerminalModelSource.Local,
        _ => throw new InvalidOperationException($"Unknown model source '{source}'."),
    };
}
