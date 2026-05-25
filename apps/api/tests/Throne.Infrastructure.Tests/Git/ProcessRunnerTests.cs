using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Application.Ports;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// Behavioural contract of <see cref="ProcessRunner"/>. The tests shell out to
/// <c>/bin/sh</c> on Unix and <c>cmd.exe</c> on Windows — both are guaranteed
/// available on every supported developer / CI host without a Docker fixture.
/// </summary>
public class ProcessRunnerTests
{
    private readonly ProcessRunner _runner = new(NullLogger<ProcessRunner>.Instance);

    [Fact(DisplayName = "RunAsync захватывает stdout и возвращает exit=0")]
    public async Task Captures_stdout_on_success()
    {
        var request = ShellEcho("hello-stdout");

        var result = await _runner.RunAsync(request, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.IsSuccess.Should().BeTrue();
        result.StandardOutput.Should().Contain("hello-stdout");
        result.StandardError.Should().BeEmpty();
    }

    [Fact(DisplayName = "RunAsync возвращает ненулевой exit и не бросает исключение")]
    public async Task NonZero_exit_does_not_throw()
    {
        var request = ShellCommand("exit 7");

        var result = await _runner.RunAsync(request, CancellationToken.None);

        result.ExitCode.Should().Be(7);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact(DisplayName = "RunAsync захватывает stderr отдельно от stdout")]
    public async Task Captures_stderr_separately()
    {
        var script = OperatingSystem.IsWindows()
            ? "echo to-err 1>&2"
            : "printf 'to-err' 1>&2";

        var result = await _runner.RunAsync(ShellCommand(script), CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.StandardError.Should().Contain("to-err");
        result.StandardOutput.Should().NotContain("to-err");
    }

    [Fact(DisplayName = "RunAsync бросает TimeoutException и убивает процесс по таймауту")]
    public async Task Timeout_kills_and_throws()
    {
        // Sleep noticeably longer than the timeout so the kill path is exercised.
        var request = ShellCommand(OperatingSystem.IsWindows() ? "ping -n 10 127.0.0.1 > nul" : "sleep 5")
            with
        { Timeout = TimeSpan.FromMilliseconds(200) };

        var act = async () => await _runner.RunAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact(DisplayName = "RunAsync прокидывает env переменные дочернему процессу")]
    public async Task Forwards_environment_variables()
    {
        var request = OperatingSystem.IsWindows()
            ? ShellCommand("echo %THRONE_TEST_VAR%")
            : ShellCommand("printf '%s' \"$THRONE_TEST_VAR\"");

        request = request with
        {
            Environment = new Dictionary<string, string?>
            {
                ["THRONE_TEST_VAR"] = "from-env",
            },
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        result.ExitCode.Should().Be(0);
        result.StandardOutput.Should().Contain("from-env");
    }

    [Fact(DisplayName = "RunAsync уважает CancellationToken и бросает OperationCanceledException")]
    public async Task Cancellation_token_aborts_run()
    {
        var request = ShellCommand(OperatingSystem.IsWindows() ? "ping -n 10 127.0.0.1 > nul" : "sleep 5");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var act = async () => await _runner.RunAsync(request, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static ProcessRunRequest ShellEcho(string text) =>
        ShellCommand(OperatingSystem.IsWindows() ? $"echo {text}" : $"printf '%s' '{text}'");

    private static ProcessRunRequest ShellCommand(string script)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessRunRequest(
                FileName: "cmd.exe",
                Arguments: ["/c", script]);
        }

        return new ProcessRunRequest(
            FileName: "/bin/sh",
            Arguments: ["-c", script]);
    }
}
