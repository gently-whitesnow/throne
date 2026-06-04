using Microsoft.Extensions.Logging;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;

namespace Throne.Infrastructure.Terminals.Capabilities;

/// <summary>
/// `tmux --version` probe for capability `terminal`. Folds binary-missing and non-zero
/// exits into <see cref="CapabilityProbeResult.Detected"/>=<c>false</c>; never throws.
/// </summary>
internal sealed class TmuxCapabilityProbe(TmuxCli tmux, ILogger<TmuxCapabilityProbe> log)
    : ICapabilityProbe
{
    public string CapabilityName => CapabilityNames.Terminal;

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var outcome = await tmux.RunAsync(["-V"], ct);
        if (!outcome.IsAvailable)
        {
            var detail = "tmux not found on PATH";
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, detail);
            return new CapabilityProbeResult(Detected: false, Detail: detail);
        }
        if (!outcome.IsSuccess || outcome.Result is null)
        {
            var detail = outcome.FailureDetail ?? "tmux -V failed";
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, detail);
            return new CapabilityProbeResult(Detected: false, Detail: detail);
        }

        var version = outcome.Result.StandardOutput.Trim();
        var stdoutDetail = string.IsNullOrEmpty(version) ? "tmux available" : version;
        TerminalsLog.CapabilityProbed(log, CapabilityName, detected: true, stdoutDetail);
        return new CapabilityProbeResult(Detected: true, Detail: stdoutDetail);
    }
}
