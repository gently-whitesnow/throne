using System.Runtime.InteropServices;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Infrastructure.Terminals;

internal sealed class AppleTerminalOpener(IProcessLauncher launcher) : ITerminalOpener, ICapabilityProbe
{
    public string ProviderName => "apple_terminal";
    public string CapabilityName => ProviderName;

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new CapabilityProbeResult(false, "Terminal.app is only available on macOS");
        }

        try
        {
            var result = await launcher.RunAsync(
                new ProcessRunRequest(
                    "osascript",
                    ["-e", "id of application \"Terminal\""],
                    Timeout: TimeSpan.FromSeconds(5)),
                ct);
            return result.IsSuccess
                ? new CapabilityProbeResult(true, result.StandardOutput.Trim())
                : new CapabilityProbeResult(false, Detail("osascript Terminal probe", result));
        }
        catch (TimeoutException)
        {
            return new CapabilityProbeResult(false, "osascript Terminal probe timed out");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CapabilityProbeResult(false, "osascript not found on PATH");
        }
    }

    public async Task OpenAsync(string intentId, string sessionName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);
        var command = NativeTmuxAttachCommand.BuildShellCommand(sessionName);
        var result = await launcher.RunAsync(
            new ProcessRunRequest(
                "osascript",
                [
                    "-e", "tell application \"Terminal\"",
                    "-e", $"do script \"{TerminalCommandEscaping.AppleScriptString(command)}\"",
                    "-e", "activate",
                    "-e", "end tell",
                ],
                Timeout: TimeSpan.FromSeconds(10)),
            ct);
        if (!result.IsSuccess)
        {
            throw new ApiException(
                TerminalErrorCodes.NativeProviderUnavailable,
                Detail("osascript Terminal launch", result),
                new Dictionary<string, object?> { ["provider"] = ProviderName });
        }
    }

    private static string Detail(string command, ProcessRunResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? $"{command} exit {result.ExitCode}"
            : result.StandardError.Trim();
}
