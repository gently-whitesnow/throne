using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.LocalModels;
using Throne.Application.Ports;
using Throne.Domain.Capabilities;
using Throne.Infrastructure.Terminals;
using Throne.Infrastructure.Terminals.Capabilities;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Validates the binary-missing fold-down rule that ADR-0026 § 1 mandates: a probe
/// must never throw — both unavailable PATH and non-zero exit collapse into
/// <see cref="CapabilityProbeResult.Detected"/> = <c>false</c>.
/// </summary>
public class CapabilityProbesTests
{
    [Fact(DisplayName = "TmuxCapabilityProbe: detected=true когда tmux -V exit 0")]
    public async Task Tmux_probe_returns_detected_on_success()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: "tmux 3.5a\n",
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        var probe = new TmuxCapabilityProbe(
            new TmuxCli(launcher, Options.Create(new TmuxOptions())),
            NullLogger<TmuxCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        probe.CapabilityName.Should().Be(CapabilityNames.Terminal);
        result.Detected.Should().BeTrue();
        result.Detail.Should().Contain("tmux 3.5a");
    }

    [Fact(DisplayName = "TmuxCapabilityProbe: detected=false если binary не найден")]
    public async Task Tmux_probe_folds_missing_binary()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new System.ComponentModel.Win32Exception("not found"));
        var probe = new TmuxCapabilityProbe(
            new TmuxCli(launcher, Options.Create(new TmuxOptions())),
            NullLogger<TmuxCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Detail.Should().Contain("not found", because: "detail повторяет launcher-сообщение");
    }

    [Fact(DisplayName = "VsCodeCapabilityProbe: detected=true когда code --version exit 0")]
    public async Task VsCode_probe_returns_detected_on_success()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Is<ProcessRunRequest>(r => r.FileName == "code"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: "1.95.3\nabcdef0\nx64\n",
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        var probe = new VsCodeCapabilityProbe(launcher, NullLogger<VsCodeCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        probe.CapabilityName.Should().Be(CapabilityNames.Vscode);
        result.Detected.Should().BeTrue();
        result.Detail.Should().Be("1.95.3");
    }

    [Fact(DisplayName = "VsCodeCapabilityProbe: detected=false если code CLI отсутствует")]
    public async Task VsCode_probe_folds_missing_binary()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new System.ComponentModel.Win32Exception("missing"));
        var probe = new VsCodeCapabilityProbe(launcher, NullLogger<VsCodeCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Detected.Should().BeFalse();
    }

    [Fact(DisplayName = "OpencodeCapabilityProbe: detected=true когда CLI и discovery=Ready")]
    public async Task Opencode_probe_detected_when_cli_and_discovery_ready()
    {
        var launcher = StubLauncherSuccess("opencode", "1.2.3\n");
        var discovery = BuildDiscovery(LocalModelDiscoveryResult.Ready("http://localhost:11435", ["m1"]));
        var probe = new OpencodeCapabilityProbe(launcher, discovery, NullLogger<OpencodeCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        probe.CapabilityName.Should().Be(CapabilityNames.Opencode);
        result.Detected.Should().BeTrue();
        result.Detail.Should().Contain("1.2.3").And.Contain("11435");
    }

    [Fact(DisplayName = "OpencodeCapabilityProbe: detected=false если CLI отсутствует — discovery не зовётся")]
    public async Task Opencode_probe_folds_missing_cli()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Is<ProcessRunRequest>(r => r.FileName == "opencode"), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new System.ComponentModel.Win32Exception("missing"));
        var catalog = Substitute.For<ILocalModelCatalogPort>();
        var discovery = new LocalModelDiscoveryService(new LocalModelSettings { BaseUrl = "http://x" }, catalog);
        var probe = new OpencodeCapabilityProbe(launcher, discovery, NullLogger<OpencodeCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Detail.Should().Contain("opencode CLI not found");
        await catalog.DidNotReceive().ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "OpencodeCapabilityProbe: CLI есть, но discovery!=Ready → detected=false с деталью")]
    public async Task Opencode_probe_folds_unreachable_discovery()
    {
        var launcher = StubLauncherSuccess("opencode", "1.2.3\n");
        var discovery = BuildDiscovery(LocalModelDiscoveryResult.NotConfigured());
        var probe = new OpencodeCapabilityProbe(launcher, discovery, NullLogger<OpencodeCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Detail.Should().Contain("LocalModel:BaseUrl");
    }

    private static IProcessLauncher StubLauncherSuccess(string fileName, string stdout)
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Is<ProcessRunRequest>(r => r.FileName == fileName), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: stdout,
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        return launcher;
    }

    private static LocalModelDiscoveryService BuildDiscovery(LocalModelDiscoveryResult planned)
    {
        var catalog = Substitute.For<ILocalModelCatalogPort>();
        if (planned.Status == LocalModelDiscoveryStatus.Ready)
        {
            catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(planned.Models));
        }
        else if (planned.Status == LocalModelDiscoveryStatus.Unreachable)
        {
            catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns<Task<IReadOnlyList<string>>>(_ => throw new InvalidOperationException(planned.Error));
        }
        else if (planned.Status == LocalModelDiscoveryStatus.Empty)
        {
            catalog.ListModelIdsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()));
        }
        var settings = new LocalModelSettings
        {
            BaseUrl = planned.Status == LocalModelDiscoveryStatus.NotConfigured ? null : planned.BaseUrl,
        };
        return new LocalModelDiscoveryService(settings, catalog);
    }
}
