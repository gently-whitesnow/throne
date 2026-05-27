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
        Prerequisite_hint = view.PrerequisiteHint,
        Detected = view.Detected,
        Detection_detail = view.DetectionDetail,
        Enabled = view.Enabled,
    };

    public static string ToDomainName(CapabilityName name) => name switch
    {
        CapabilityName.Repositories => CapabilityNames.Repositories,
        CapabilityName.Terminal => CapabilityNames.Terminal,
        CapabilityName.Vscode => CapabilityNames.Vscode,
        _ => throw new ArgumentOutOfRangeException(nameof(name), $"Unknown capability name '{name}'."),
    };

    private static CapabilityName ParseName(string domain) => domain switch
    {
        CapabilityNames.Repositories => CapabilityName.Repositories,
        CapabilityNames.Terminal => CapabilityName.Terminal,
        CapabilityNames.Vscode => CapabilityName.Vscode,
        _ => throw new InvalidOperationException(
            $"Capability '{domain}' is not exposed in the OpenAPI contract (extend `CapabilityName` first)."),
    };
}
