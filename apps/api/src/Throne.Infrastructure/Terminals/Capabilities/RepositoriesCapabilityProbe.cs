using Microsoft.Extensions.Logging;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Terminals.Capabilities;

/// <summary>
/// Reuses the Slice 1 <see cref="GhAuthProbe"/> to back the `repositories` capability
/// detection. ADR-0026 § 1 mandates no duplicate detector: the same shell-out we drive
/// for the settings repositories card serves the capability registry.
/// </summary>
internal sealed class RepositoriesCapabilityProbe(GhAuthProbe gh, ILogger<RepositoriesCapabilityProbe> log)
    : ICapabilityProbe
{
    public string CapabilityName => CapabilityNames.Repositories;

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var status = await gh.ProbeAsync(ct);
        var detected = status.IsAuthenticated;
        var detail = detected
            ? string.IsNullOrWhiteSpace(status.Account)
                ? $"gh authenticated on {status.Host}"
                : $"gh as {status.Account} on {status.Host}"
            : string.IsNullOrWhiteSpace(status.Detail)
                ? "gh not authenticated"
                : status.Detail;

        TerminalsLog.CapabilityProbed(log, CapabilityName, detected, detail);
        return new CapabilityProbeResult(detected, detail);
    }
}
