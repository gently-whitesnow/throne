using FluentAssertions;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class TerminalOpenersTests
{
    [Fact(DisplayName = "WezTerm opener запускает wezterm start -- tmux attach -d -t <session>")]
    public async Task WezTerm_open_builds_expected_argv()
    {
        var launcher = SuccessLauncher();
        var opener = new WezTermOpener(launcher);

        await opener.OpenAsync("intent-1", "throne-intent-1", CancellationToken.None);

        var request = (ProcessRunRequest)launcher.ReceivedCalls().Single().GetArguments()[0]!;
        request.FileName.Should().Be("wezterm");
        request.Arguments.Should().Equal(
            "start", "--", "tmux", "attach", "-d", "-t", "throne-intent-1");
    }

    [Fact(DisplayName = "Terminal.app opener запускает osascript do script с tmux attach -d")]
    public async Task AppleTerminal_open_builds_expected_argv()
    {
        var launcher = SuccessLauncher();
        var opener = new AppleTerminalOpener(launcher);

        await opener.OpenAsync("intent-1", "throne-intent-1", CancellationToken.None);

        var request = (ProcessRunRequest)launcher.ReceivedCalls().Single().GetArguments()[0]!;
        request.FileName.Should().Be("osascript");
        request.Arguments.Should().Equal(
            "-e", "tell application \"Terminal\"",
            "-e", "do script \"tmux attach -d -t 'throne-intent-1'\"",
            "-e", "activate",
            "-e", "end tell");
    }

    [Fact(DisplayName = "TerminalCommandEscaping экранирует shell и AppleScript уровни отдельно")]
    public void Terminal_command_escaping_is_layered()
    {
        var shell = TerminalCommandEscaping.ShellSingleQuote("throne-a'b");
        shell.Should().Be("'throne-a'\\''b'");

        var apple = TerminalCommandEscaping.AppleScriptString("tmux \"x\" \\ y");
        apple.Should().Be("tmux \\\"x\\\" \\\\ y");
    }

    private static IProcessLauncher SuccessLauncher()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        return launcher;
    }
}
