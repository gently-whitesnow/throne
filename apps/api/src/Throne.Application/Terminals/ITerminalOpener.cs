using Throne.Application.Terminals.Capabilities;

namespace Throne.Application.Terminals;

public interface ITerminalOpener
{
    string ProviderName { get; }

    Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct);

    Task OpenAsync(string intentId, string sessionName, CancellationToken ct);
}
