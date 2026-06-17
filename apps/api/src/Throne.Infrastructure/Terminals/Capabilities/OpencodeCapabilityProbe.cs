using Microsoft.Extensions.Logging;
using Throne.Application.LocalModels;
using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;

namespace Throne.Infrastructure.Terminals.Capabilities;

/// <summary>
/// `opencode --version` + local OpenAI-compatible endpoint probe for capability `opencode`.
/// Detected only when both halves succeed: the CLI is on PATH AND
/// <see cref="LocalModelDiscoveryService"/> resolves to <see cref="LocalModelDiscoveryStatus.Ready"/>.
/// Same fold-down rules as the other probes — never throws on a missing binary or an
/// unreachable endpoint.
/// </summary>
internal sealed class OpencodeCapabilityProbe(
    IProcessLauncher launcher,
    LocalModelDiscoveryService discovery,
    ILogger<OpencodeCapabilityProbe> log) : ICapabilityProbe
{
    public string CapabilityName => CapabilityNames.Opencode;

    public async Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
    {
        var cli = await ProbeCliAsync(ct);
        if (!cli.Available)
        {
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, cli.Detail);
            return new CapabilityProbeResult(Detected: false, Detail: cli.Detail);
        }

        var models = await discovery.DiscoverAsync(ct);
        if (models.Status != LocalModelDiscoveryStatus.Ready)
        {
            var detail = $"{cli.Detail}; local models: {DescribeDiscovery(models)}";
            TerminalsLog.CapabilityProbed(log, CapabilityName, detected: false, detail);
            return new CapabilityProbeResult(Detected: false, Detail: detail);
        }

        var ready = $"{cli.Detail}; {models.Models.Count} model(s) at {models.BaseUrl}";
        TerminalsLog.CapabilityProbed(log, CapabilityName, detected: true, ready);
        return new CapabilityProbeResult(Detected: true, Detail: ready);
    }

    private async Task<CliOutcome> ProbeCliAsync(CancellationToken ct)
    {
        var request = new ProcessRunRequest(
            FileName: "opencode",
            Arguments: ["--version"],
            Timeout: TimeSpan.FromSeconds(5));

        ProcessRunResult result;
        try
        {
            result = await launcher.RunAsync(request, ct);
        }
        catch (TimeoutException)
        {
            return new CliOutcome(false, "opencode --version timed out");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return new CliOutcome(false, "opencode CLI not found on PATH");
        }

        if (result.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(result.StandardError)
                ? $"opencode --version exit {result.ExitCode}"
                : result.StandardError.Trim();
            return new CliOutcome(false, detail);
        }

        var version = result.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "opencode available";
        return new CliOutcome(true, version);
    }

    private static string DescribeDiscovery(LocalModelDiscoveryResult result) => result.Status switch
    {
        LocalModelDiscoveryStatus.NotConfigured => "Throne:LocalModel:BaseUrl is not configured",
        LocalModelDiscoveryStatus.Empty => $"endpoint {result.BaseUrl} returned no models",
        LocalModelDiscoveryStatus.Unreachable => $"endpoint {result.BaseUrl} unreachable ({result.Error})",
        _ => result.Status.ToString(),
    };

    private readonly record struct CliOutcome(bool Available, string Detail);
}
