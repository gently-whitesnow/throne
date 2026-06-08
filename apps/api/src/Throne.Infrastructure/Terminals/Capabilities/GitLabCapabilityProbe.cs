using Microsoft.Extensions.Logging;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Terminals.Capabilities;

internal sealed class GitLabCapabilityProbe(GlabAuthProbe glab, ILogger<GitLabCapabilityProbe> log)
    : ICapabilityProbe
{
    public string CapabilityName => CapabilityNames.Gitlab;

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var status = await glab.ProbeAsync(ct);
        var detected = status.IsAuthenticated;
        var detail = detected
            ? string.IsNullOrWhiteSpace(status.Account)
                ? $"glab authenticated on {status.Host}"
                : $"glab as {status.Account} on {status.Host}"
            : string.IsNullOrWhiteSpace(status.Detail)
                ? "glab not authenticated"
                : status.Detail;

        TerminalsLog.CapabilityProbed(log, CapabilityName, detected, detail);
        return new CapabilityProbeResult(detected, detail);
    }
}
