using FluentAssertions;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals.Capabilities;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Validates the provider-neutral login probe core: process-launch <c>Win32Exception</c>
/// → <see cref="AgentVendorLoginStatus.Missing"/>; exit 0 → <see cref="AgentVendorLoginStatus.Ready"/>;
/// any other exit → <see cref="AgentVendorLoginStatus.LoggedOut"/>. Must never throw.
/// </summary>
public class CliLoginProbesTests
{
    [Fact(DisplayName = "ClaudeLoginProbe: status exit 0 → Ready, читаемая первая строка как detail")]
    public async Task Claude_probe_ready_on_exit_zero()
    {
        var launcher = StubLauncher("claude", exitCode: 0, stdout: "Logged in as ada@example.com\n");
        var probe = new ClaudeLoginProbe(launcher);

        var result = await probe.ProbeAsync(CancellationToken.None);

        probe.Vendor.Should().Be(TerminalAgentCatalog.VendorClaude);
        result.Status.Should().Be(AgentVendorLoginStatus.Ready);
        result.Detail.Should().Be("Logged in as ada@example.com");
    }

    [Fact(DisplayName = "ClaudeLoginProbe: JSON-вывод не утекает в detail (Ready, detail=null)")]
    public async Task Claude_probe_drops_json_detail()
    {
        var launcher = StubLauncher("claude", exitCode: 0, stdout: "{\"email\":\"ada@example.com\"}\n");
        var probe = new ClaudeLoginProbe(launcher);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Status.Should().Be(AgentVendorLoginStatus.Ready);
        result.Detail.Should().BeNull();
    }

    [Fact(DisplayName = "CodexLoginProbe: exit !=0 → LoggedOut с подсказкой login")]
    public async Task Codex_probe_logged_out_on_nonzero_exit()
    {
        var launcher = StubLauncher("codex", exitCode: 1, stdout: string.Empty);
        var probe = new CodexLoginProbe(launcher);

        var result = await probe.ProbeAsync(CancellationToken.None);

        probe.Vendor.Should().Be(TerminalAgentCatalog.VendorCodex);
        result.Status.Should().Be(AgentVendorLoginStatus.LoggedOut);
        result.Detail.Should().Be("codex login");
    }

    [Fact(DisplayName = "Login probe: CLI отсутствует (Win32Exception) → Missing, не бросает")]
    public async Task Probe_folds_missing_cli()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new System.ComponentModel.Win32Exception("not found"));
        var probe = new ClaudeLoginProbe(launcher);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Status.Should().Be(AgentVendorLoginStatus.Missing);
        result.Detail.Should().Contain("claude");
    }

    [Fact(DisplayName = "Login probe: таймаут статуса → LoggedOut, не бросает")]
    public async Task Probe_folds_timeout()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new TimeoutException());
        var probe = new CodexLoginProbe(launcher);

        var result = await probe.ProbeAsync(CancellationToken.None);

        result.Status.Should().Be(AgentVendorLoginStatus.LoggedOut);
    }

    private static IProcessLauncher StubLauncher(string fileName, int exitCode, string stdout)
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Is<ProcessRunRequest>(r => r.FileName == fileName), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: exitCode,
                StandardOutput: stdout,
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        return launcher;
    }
}
