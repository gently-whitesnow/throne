using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;

namespace Throne.Infrastructure.Terminals;

internal sealed class WezTermOpener(IProcessLauncher launcher) : ITerminalOpener, ICapabilityProbe
{
    public string ProviderName => "wezterm";
    public string CapabilityName => ProviderName;

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        try
        {
            var result = await launcher.RunAsync(
                new ProcessRunRequest("wezterm", ["--version"], Timeout: TimeSpan.FromSeconds(5)),
                ct);
            if (!result.IsSuccess)
            {
                return new CapabilityProbeResult(false, Detail("wezterm --version", result));
            }
            var firstLine = result.StandardOutput
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? "wezterm available";
            return new CapabilityProbeResult(true, firstLine);
        }
        catch (TimeoutException)
        {
            return new CapabilityProbeResult(false, "wezterm --version timed out");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CapabilityProbeResult(false, "wezterm CLI not found on PATH");
        }
    }

    public async Task OpenAsync(string intentId, string sessionName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionName);
        var result = await launcher.RunAsync(
            new ProcessRunRequest(
                "wezterm",
                ["start", "--", "tmux", "attach", "-d", "-t", sessionName],
                Timeout: TimeSpan.FromSeconds(10)),
            ct);
        if (!result.IsSuccess)
        {
            throw new ApiException(
                TerminalErrorCodes.NativeProviderUnavailable,
                Detail("wezterm start", result),
                new Dictionary<string, object?> { ["provider"] = ProviderName });
        }
    }

    private static string Detail(string command, ProcessRunResult result) =>
        string.IsNullOrWhiteSpace(result.StandardError)
            ? $"{command} exit {result.ExitCode}"
            : result.StandardError.Trim();
}
