using Microsoft.Extensions.Logging;
using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Infrastructure.Terminals.Capabilities;

/// <summary>
/// `cursor --version` probe for the Cursor IDE provider under capability
/// <c>open_in_ide</c>. Same folding rules as <see cref="VsCodeCapabilityProbe"/>:
/// binary missing / non-zero exit / timeout all collapse to <c>detected=false</c>.
/// </summary>
internal sealed class CursorCapabilityProbe(IProcessLauncher launcher, ILogger<CursorCapabilityProbe> log)
    : ICapabilityProbe
{
    public string CapabilityName => "cursor";

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var request = new ProcessRunRequest(
            FileName: "cursor",
            Arguments: ["--version"],
            Timeout: TimeSpan.FromSeconds(5));

        ProcessRunResult result;
        try
        {
            result = await launcher.RunAsync(request, ct);
        }
        catch (TimeoutException ex)
        {
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, ex.Message);
            return new CapabilityProbeResult(Detected: false, Detail: "cursor --version timed out");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            const string detail = "cursor CLI not found on PATH";
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, detail);
            return new CapabilityProbeResult(Detected: false, Detail: detail);
        }

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"cursor --version exit {result.ExitCode}"
                : result.StandardError.Trim();
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, detail);
            return new CapabilityProbeResult(Detected: false, Detail: detail);
        }

        var firstLine = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "cursor available";
        TerminalsLog.CapabilityProbed(log, CapabilityName, detected: true, firstLine);
        return new CapabilityProbeResult(Detected: true, Detail: firstLine);
    }
}
