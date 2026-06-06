using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;

namespace Throne.Application.Vscode;

/// <summary>
/// Capability gate for the VS Code shell-out endpoints. Both the persisted toggle
/// (`capabilities.vscode.enabled`) and the live `code --version` probe must pass.
/// </summary>
public sealed class VscodeCapabilityGuard(
    ICapabilitiesRepository capabilities,
    ICapabilityDetectionCache detection)
{
    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        var stored = await capabilities.GetAsync(ct);
        if (stored is null || !stored.IsEnabled(CapabilityNames.Vscode))
        {
            throw VscodeFailures.CapabilityDisabled();
        }

        var probe = await detection.GetAsync(CapabilityNames.Vscode, ct);
        if (probe is null || !probe.Detected)
        {
            throw VscodeFailures.CapabilityUndetected();
        }
    }
}
