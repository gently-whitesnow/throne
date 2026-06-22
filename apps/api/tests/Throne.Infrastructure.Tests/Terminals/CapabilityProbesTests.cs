using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Ports;
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
    [Fact(DisplayName = "TmuxCapabilityProbe: detected=true when tmux -V exits 0; capability key is the literal provider name")]
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

        probe.CapabilityName.Should().Be("tmux");
        result.Detected.Should().BeTrue();
        result.Detail.Should().Contain("tmux 3.5a");
    }

    [Fact(DisplayName = "TmuxCapabilityProbe: detected=false when the binary is missing")]
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
        result.Detail.Should().Contain("not found");
    }

    [Fact(DisplayName = "VsCodeCapabilityProbe: detected=true when code --version exits 0; key is literal vscode")]
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

        probe.CapabilityName.Should().Be("vscode");
        result.Detected.Should().BeTrue();
        result.Detail.Should().Be("1.95.3");
    }

    [Fact(DisplayName = "VsCodeCapabilityProbe: detected=false when the code CLI is missing")]
    public async Task VsCode_probe_folds_missing_binary()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new System.ComponentModel.Win32Exception("missing"));
        var probe = new VsCodeCapabilityProbe(launcher, NullLogger<VsCodeCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Detected.Should().BeFalse();
    }

    [Fact(DisplayName = "CursorCapabilityProbe: detected=true when cursor --version exits 0; key is literal cursor")]
    public async Task Cursor_probe_returns_detected_on_success()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Is<ProcessRunRequest>(r => r.FileName == "cursor"), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: "Cursor 0.42.0\n",
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        var probe = new CursorCapabilityProbe(launcher, NullLogger<CursorCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        probe.CapabilityName.Should().Be("cursor");
        result.Detected.Should().BeTrue();
        result.Detail.Should().Contain("Cursor 0.42.0");
    }

    [Fact(DisplayName = "CursorCapabilityProbe: detected=false when the cursor CLI is missing")]
    public async Task Cursor_probe_folds_missing_binary()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Is<ProcessRunRequest>(r => r.FileName == "cursor"), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new System.ComponentModel.Win32Exception("missing"));
        var probe = new CursorCapabilityProbe(launcher, NullLogger<CursorCapabilityProbe>.Instance);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Detected.Should().BeFalse();
    }
}
