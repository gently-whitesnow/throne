using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

/// <summary>
/// Projects the static <see cref="TerminalAgentCatalog"/> into the wire DTO for
/// <c>GET /api/v1/terminal/vendors</c>. Backend stays the single source of truth for vendor
/// metadata and curated model lists; the frontend reads this instead of mirroring the catalog.
/// </summary>
internal static class TerminalVendorCatalogMapper
{
    public static TerminalVendorCatalogResponse ToDto() => new()
    {
        Default_vendor = ParseVendor(TerminalAgentCatalog.DefaultVendor),
        Vendors = TerminalAgentCatalog.Descriptors.Select(ToVendorDto).ToArray(),
    };

    private static TerminalVendorMetadataDto ToVendorDto(TerminalVendorDescriptor descriptor) => new()
    {
        Vendor = ParseVendor(descriptor.Vendor),
        Label = descriptor.Label,
        Supports_effort = descriptor.SupportsEffort,
        Models = descriptor.Models.ToArray(),
        Default_model = descriptor.DefaultModel,
        Efforts = descriptor.Efforts.ToArray(),
        Default_effort = descriptor.DefaultEffort is { } effort ? ParseEffort(effort) : null,
        Model_source = ParseModelSource(descriptor.ModelSource),
    };

    private static TerminalAgentVendor ParseVendor(string vendor) => vendor switch
    {
        TerminalAgentCatalog.VendorClaude => TerminalAgentVendor.Claude,
        TerminalAgentCatalog.VendorCodex => TerminalAgentVendor.Codex,
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
        _ => throw new InvalidOperationException($"Unknown model source '{source}'."),
    };
}
