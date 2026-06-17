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
        CapabilityName.Gitlab => CapabilityNames.Gitlab,
        CapabilityName.Terminal => CapabilityNames.Terminal,
        CapabilityName.Vscode => CapabilityNames.Vscode,
        CapabilityName.Opencode => CapabilityNames.Opencode,
        _ => throw new ArgumentOutOfRangeException(nameof(name), $"Unknown capability name '{name}'."),
    };

    private static CapabilityName ParseName(string domain) => domain switch
    {
        CapabilityNames.Repositories => CapabilityName.Repositories,
        CapabilityNames.Gitlab => CapabilityName.Gitlab,
        CapabilityNames.Terminal => CapabilityName.Terminal,
        CapabilityNames.Vscode => CapabilityName.Vscode,
        CapabilityNames.Opencode => CapabilityName.Opencode,
        _ => throw new InvalidOperationException(
            $"Capability '{domain}' is not exposed in the OpenAPI contract (extend `CapabilityName` first)."),
    };
}
