using Microsoft.Extensions.Logging;
using Throne.Application.Ide;
using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Infrastructure.Ide;

/// <summary>
/// IDE opener for the Cursor provider under capability <c>open_in_ide</c>.
/// Mirrors <see cref="VsCodeOpener"/>: probe + open share the <c>cursor</c>
/// binary and degrade safely when the CLI is absent.
/// </summary>
internal sealed class CursorOpener(IProcessLauncher launcher, ILogger<CursorOpener> log) : IIdeOpener
{
    public string ProviderName => "cursor";

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var request = new ProcessRunRequest(
            FileName: "cursor",
            Arguments: ["--version"],
            Timeout: TimeSpan.FromSeconds(5));
        try
        {
            var result = await launcher.RunAsync(request, ct);
            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"cursor --version exit {result.ExitCode}"
                    : result.StandardError.Trim();
                return new CapabilityProbeResult(Detected: false, Detail: detail);
            }
            var firstLine = result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? "cursor available";
            return new CapabilityProbeResult(Detected: true, Detail: firstLine);
        }
        catch (TimeoutException ex)
        {
            return new CapabilityProbeResult(Detected: false, Detail: ex.Message);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CapabilityProbeResult(Detected: false, Detail: "cursor CLI not found on PATH");
        }
    }

    public async Task OpenAsync(string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var request = new ProcessRunRequest(
            FileName: "cursor",
            Arguments: [workspacePath],
            Timeout: TimeSpan.FromSeconds(10));
        var result = await launcher.RunAsync(request, ct);
        if (!result.IsSuccess)
        {
            IdeLog.OpenFailed(log, ProviderName, result.ExitCode, workspacePath, result.StandardError);
        }
    }
}
