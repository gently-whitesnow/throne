using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Unit-level checks of <see cref="TmuxSessionManager"/> against a substituted
/// <see cref="IProcessLauncher"/>. Verifies the shell-out vector, the binary-missing
/// fold-down, and the spawn → has-session double-check.
/// </summary>
public class TmuxSessionManagerTests
{
    private const string IntentId = "intent-abc";

    [Fact(DisplayName = "Spawn собирает tmux new-session -ADs throne-<id> ... -- {command} {args}")]
    public async Task Spawn_builds_expected_argv()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        SetupLauncherSuccess(launcher);
        var sut = NewManager(launcher);

        var result = await sut.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId,
                "/Users/me/workspace/intent-abc",
                "claude",
                ["--prompt", "hello"]),
            CancellationToken.None);

        result.SessionName.Should().Be("throne-intent-abc");
        result.IsAlive.Should().BeTrue();

        var allArgs = launcher
            .ReceivedCalls()
            .Select(c => (ProcessRunRequest)c.GetArguments()[0]!)
            .ToArray();

        allArgs[0].FileName.Should().Be("tmux");
        allArgs[0].Arguments.Should().Equal(
            "new-session", "-A", "-D",
            "-s", "throne-intent-abc",
            "-c", "/Users/me/workspace/intent-abc",
            "-d",
            "claude", "--prompt", "hello");
        allArgs[1].Arguments.Should().Equal("has-session", "-t", "throne-intent-abc");
    }

    [Fact(DisplayName = "Spawn с EnableMouse=true дёргает set-option … mouse on после has-session")]
    public async Task Spawn_enables_mouse_when_requested()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        SetupLauncherSuccess(launcher);
        var sut = NewManager(launcher);

        await sut.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId,
                "/Users/me/workspace/intent-abc",
                "opencode",
                [],
                EnableMouse: true),
            CancellationToken.None);

        var argvs = launcher
            .ReceivedCalls()
            .Select(c => ((ProcessRunRequest)c.GetArguments()[0]!).Arguments)
            .ToArray();

        argvs.Should().HaveCount(3);
        argvs[1].Should().Equal("has-session", "-t", "throne-intent-abc");
        argvs[2].Should().Equal("set-option", "-t", "throne-intent-abc", "mouse", "on");
    }

    [Fact(DisplayName = "Spawn прокидывает env через tmux -e до команды")]
    public async Task Spawn_injects_environment_variables()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        SetupLauncherSuccess(launcher);
        var sut = NewManager(launcher);

        await sut.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId,
                "/Users/me/workspace/intent-abc",
                "claude",
                ["--prompt", "hello"],
                EnvironmentVariables: new Dictionary<string, string>
                {
                    ["THRONE_INTENT_ID"] = IntentId,
                    ["THRONE_API_BASE"] = "http://localhost:5008",
                }),
            CancellationToken.None);

        var argv = (ProcessRunRequest)launcher.ReceivedCalls().First().GetArguments()[0]!;
        argv.Arguments.Should().Equal(
            "new-session", "-A", "-D",
            "-s", "throne-intent-abc",
            "-c", "/Users/me/workspace/intent-abc",
            "-d",
            "-e", "THRONE_API_BASE=http://localhost:5008",
            "-e", "THRONE_INTENT_ID=intent-abc",
            "claude", "--prompt", "hello");
    }

    [Fact(DisplayName = "Spawn без EnableMouse не трогает set-option (default)")]
    public async Task Spawn_does_not_touch_mouse_by_default()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        SetupLauncherSuccess(launcher);
        var sut = NewManager(launcher);

        await sut.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId,
                "/Users/me/workspace/intent-abc",
                "claude",
                []),
            CancellationToken.None);

        var argvs = launcher
            .ReceivedCalls()
            .Select(c => ((ProcessRunRequest)c.GetArguments()[0]!).Arguments)
            .ToArray();

        argvs.Should().HaveCount(2);
        argvs.Should().NotContain(a => a.Contains("set-option"));
    }

    [Fact(DisplayName = "HasSession возвращает false если tmux not on PATH")]
    public async Task HasSession_folds_binary_missing_to_false()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ProcessRunResult>>(_ => throw new System.ComponentModel.Win32Exception("not found"));
        var sut = NewManager(launcher);

        var alive = await sut.HasSessionAsync(IntentId, CancellationToken.None);

        alive.Should().BeFalse();
    }

    [Fact(DisplayName = "KillSession возвращает false если session отсутствует (exit != 0)")]
    public async Task KillSession_returns_false_when_session_missing()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 1,
                StandardOutput: string.Empty,
                StandardError: "can't find session: throne-intent-abc",
                Elapsed: TimeSpan.Zero)));
        var sut = NewManager(launcher);

        var killed = await sut.KillSessionAsync(IntentId, CancellationToken.None);

        killed.Should().BeFalse();
    }

    [Fact(DisplayName = "KillSession делает has-session до и после kill для диагностики")]
    public async Task KillSession_probes_has_session_around_kill()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        SetupLauncherSuccess(launcher);
        var sut = NewManager(launcher);

        await sut.KillSessionAsync(IntentId, CancellationToken.None);

        var argvs = launcher
            .ReceivedCalls()
            .Select(c => ((ProcessRunRequest)c.GetArguments()[0]!).Arguments)
            .ToArray();

        argvs.Should().HaveCount(3);
        argvs[0].Should().Equal("has-session", "-t", "throne-intent-abc");
        argvs[1].Should().Equal("kill-session", "-t", "throne-intent-abc");
        argvs[2].Should().Equal("has-session", "-t", "throne-intent-abc");
    }

    [Fact(DisplayName = "KillSession эмитит TerminalSessionStopped при успехе и молчит при провале")]
    public async Task KillSession_emits_stopped_only_on_success()
    {
        var events = Substitute.For<IDomainEventDispatcher>();

        var okLauncher = Substitute.For<IProcessLauncher>();
        SetupLauncherSuccess(okLauncher);
        await NewManager(okLauncher, events).KillSessionAsync(IntentId, CancellationToken.None);

        await events.Received(1).DispatchAsync(
            Arg.Is<TerminalSessionStopped>(e => e.IntentId == IntentId), Arg.Any<CancellationToken>());

        events.ClearReceivedCalls();
        var failLauncher = Substitute.For<IProcessLauncher>();
        failLauncher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 1,
                StandardOutput: string.Empty,
                StandardError: "no session",
                Elapsed: TimeSpan.Zero)));
        await NewManager(failLauncher, events).KillSessionAsync(IntentId, CancellationToken.None);

        await events.DidNotReceive().DispatchAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CapturePaneAsync шеллится в tmux capture-pane -p -t throne-<id> и возвращает stdout как есть")]
    public async Task Capture_pane_returns_stdout()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: "╭─╮\n│ > _ │\n╰─╯",
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        var sut = NewManager(launcher);

        var snapshot = await sut.CapturePaneAsync(IntentId, CancellationToken.None);

        snapshot.Should().Be("╭─╮\n│ > _ │\n╰─╯");
        var argv = (ProcessRunRequest)launcher.ReceivedCalls().Single().GetArguments()[0]!;
        argv.Arguments.Should().Equal("capture-pane", "-p", "-t", "throne-intent-abc");
    }

    [Fact(DisplayName = "CapturePaneAsync глотает non-zero exit и возвращает пустую строку для безопасной диагностики")]
    public async Task Capture_pane_returns_empty_on_failure()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 1,
                StandardOutput: string.Empty,
                StandardError: "no session",
                Elapsed: TimeSpan.Zero)));
        var sut = NewManager(launcher);

        var snapshot = await sut.CapturePaneAsync(IntentId, CancellationToken.None);

        snapshot.Should().BeEmpty();
    }

    [Fact(DisplayName = "ListThroneSessions фильтрует список по префиксу 'throne-'")]
    public async Task ListThroneSessions_filters_prefix()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: "throne-a\nthrone-b\nlocal-other\n",
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));
        var sut = NewManager(launcher);

        var sessions = await sut.ListThroneSessionsAsync(CancellationToken.None);

        sessions.Should().BeEquivalentTo(["throne-a", "throne-b"]);
    }

    private static void SetupLauncherSuccess(IProcessLauncher launcher) =>
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(
                ExitCode: 0,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                Elapsed: TimeSpan.Zero)));

    private static TmuxSessionManager NewManager(IProcessLauncher launcher) =>
        NewManager(launcher, Substitute.For<IDomainEventDispatcher>());

    private static TmuxSessionManager NewManager(IProcessLauncher launcher, IDomainEventDispatcher events)
    {
        var options = Options.Create(new TmuxOptions());
        var cli = new TmuxCli(launcher, options);
        return new TmuxSessionManager(
            cli, NullLogger<TmuxSessionManager>.Instance, events);
    }
}
