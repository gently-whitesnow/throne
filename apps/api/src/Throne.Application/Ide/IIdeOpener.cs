using Throne.Application.Terminals.Capabilities;

namespace Throne.Application.Ide;

/// <summary>
/// Single IDE-opener provider (VS Code, Cursor, ...). One implementation per stable
/// provider key. <see cref="ProbeAsync"/> doubles as the capability probe registered with
/// <see cref="ICapabilityDetectionCache"/> under the same <see cref="ProviderName"/> key.
/// </summary>
public interface IIdeOpener
{
    string ProviderName { get; }

    Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct);

    Task OpenAsync(string workspacePath, CancellationToken ct);
}
