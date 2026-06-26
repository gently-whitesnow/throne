using Throne.Application.Terminals.Capabilities;
using Throne.Capabilities.Contracts.Generated;
using Throne.Domain.Capabilities;

namespace Throne.Api.Settings;

/// <summary>
/// View ↔ wire DTO mapping for the capabilities surface. Keeps the controller and the
/// application service free of NSwag-generated types.
/// </summary>
internal static class CapabilityDtoMapper
{
    public static CapabilityDto ToDto(CapabilityView view) => new()
    {
        Name = ParseName(view.Name),
        Title = view.Title,
        Description = view.Description,
        Selected_provider = view.SelectedProvider,
        Providers = view.Providers.Select(ToDto).ToArray(),
    };

    public static CapabilityProviderDto ToDto(CapabilityProviderView view) => new()
    {
        Name = view.Name,
        Title = view.Title,
        Prerequisite_hint = view.PrerequisiteHint,
        Detected = view.Detected,
        Detection_detail = view.DetectionDetail,
    };

    public static string ToDomainName(CapabilityName name) => name switch
    {
        CapabilityName.Open_in_ide => CapabilityNames.OpenInIde,
        CapabilityName.Open_in_terminal => CapabilityNames.OpenInTerminal,
        _ => throw new ArgumentOutOfRangeException(nameof(name), $"Unknown capability name '{name}'."),
    };

    private static CapabilityName ParseName(string domain) => domain switch
    {
        CapabilityNames.OpenInIde => CapabilityName.Open_in_ide,
        CapabilityNames.OpenInTerminal => CapabilityName.Open_in_terminal,
        _ => throw new InvalidOperationException(
            $"Capability '{domain}' is not exposed in the OpenAPI contract (extend `CapabilityName` first)."),
    };
}
