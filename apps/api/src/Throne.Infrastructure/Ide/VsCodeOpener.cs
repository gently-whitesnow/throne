using Microsoft.Extensions.Logging;
using Throne.Application.Ide;
using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Infrastructure.Ide;

/// <summary>
/// IDE opener for the VS Code provider under capability <c>open_in_ide</c>.
/// Spawns <c>code &lt;path&gt;</c> via the shared <see cref="IProcessLauncher"/>.
/// Probe / open use the same binary; absence of the CLI folds to
/// <c>detected=false</c> rather than throwing.
/// </summary>
internal sealed class VsCodeOpener(IProcessLauncher launcher, ILogger<VsCodeOpener> log) : IIdeOpener
{
    public string ProviderName => "vscode";

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var request = new ProcessRunRequest(
            FileName: "code",
            Arguments: ["--version"],
            Timeout: TimeSpan.FromSeconds(5));
        try
        {
            var result = await launcher.RunAsync(request, ct);
            if (result.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(result.StandardError)
                    ? $"code --version exit {result.ExitCode}"
                    : result.StandardError.Trim();
                return new CapabilityProbeResult(Detected: false, Detail: detail);
            }
            var firstLine = result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? "code available";
            return new CapabilityProbeResult(Detected: true, Detail: firstLine);
        }
        catch (TimeoutException ex)
        {
            return new CapabilityProbeResult(Detected: false, Detail: ex.Message);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CapabilityProbeResult(Detected: false, Detail: "code CLI not found on PATH");
        }
    }

    public async Task OpenAsync(string workspacePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var request = new ProcessRunRequest(
            FileName: "code",
            Arguments: [workspacePath],
            Timeout: TimeSpan.FromSeconds(10));
        var result = await launcher.RunAsync(request, ct);
        if (!result.IsSuccess)
        {
            IdeLog.OpenFailed(log, ProviderName, result.ExitCode, workspacePath, result.StandardError);
        }
    }
}
