using Microsoft.Extensions.Logging;
using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Infrastructure.Terminals.Capabilities;

/// <summary>
/// `code --version` probe for provider <c>vscode</c> under capability
/// <c>open_in_ide</c>. The cache keys probes by <see cref="CapabilityName"/>; for
/// carrier capabilities this is the provider name, not the capability key.
/// </summary>
internal sealed class VsCodeCapabilityProbe(IProcessLauncher launcher, ILogger<VsCodeCapabilityProbe> log)
    : ICapabilityProbe
{
    public string CapabilityName => "vscode";

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var request = new ProcessRunRequest(
            FileName: "code",
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
            return new CapabilityProbeResult(Detected: false, Detail: "code --version timed out");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            const string detail = "code CLI not found on PATH";
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, detail);
            return new CapabilityProbeResult(Detected: false, Detail: detail);
        }

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"code --version exit {result.ExitCode}"
                : result.StandardError.Trim();
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, detail);
            return new CapabilityProbeResult(Detected: false, Detail: detail);
        }

        // First line of `code --version` is the semver (e.g. "1.95.3"); the rest is
        // commit hash + arch. Keeping just the first line keeps the UI tooltip tidy.
        var firstLine = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "code available";
        TerminalsLog.CapabilityProbed(log, CapabilityName, detected: true, firstLine);
        return new CapabilityProbeResult(Detected: true, Detail: firstLine);
    }
}
