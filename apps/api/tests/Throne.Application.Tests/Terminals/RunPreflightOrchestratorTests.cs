using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Terminals;

public partial class RunPreflightOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 10, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-run-1";
    private const string WorkspaceRoot = "/tmp/throne-test-workspaces";
    private const string SettingsPath = $"{WorkspaceRoot}/intents/{IntentIdValue}/throne-session.settings.json";
    private static readonly TerminalLaunchInput DefaultLaunch = new(Vendor: null, Model: null, Effort: null);
    private static readonly TerminalSpawnPrompt NoPrompt = TerminalSpawnPrompt.Empty;
    private static readonly TerminalSpawnPrompt CuratedPrompt = new("RULES", "TASK", null, null);
    private static readonly string[] ClaudeBareArgs = ["--model", "opus", "--effort", "high", "--settings", SettingsPath];

    [Fact(DisplayName = "Run падает с capability.disabled, если capability terminal выключен")]
    public async Task Run_capability_disabled_throws()
    {
        var fixture = new Fixture().Setup(intentExists: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, DefaultLaunch, NoPrompt, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.CapabilityDisabled);
        await fixture.Tmux.DidNotReceive().SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Run на неизвестный mode бросает terminal.mode_invalid")]
    public async Task Run_invalid_mode_throws()
    {
        var fixture = new Fixture().Setup(capabilityEnabled: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, "interactive", DefaultLaunch, NoPrompt, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalModeInvalid);
    }

    [Fact(DisplayName = "Run без живой сессии и готовых клонов спавнит tmux и paste-ит задачу в pane")]
    public async Task Run_spawns_when_all_bindings_ready()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult(TmuxSessionName.For(IntentIdValue), IsAlive: true, Detail: null));

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Work, DefaultLaunch, CuratedPrompt, restart: false, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Running);
        result.SessionName.Should().Be($"throne-{IntentIdValue}");
        result.BlockingBindings.Should().BeEmpty();
        await fixture.Tmux.Received(1).SpawnAsync(
            Arg.Is<TmuxSpawnRequest>(r =>
                r.Command == "claude"
                && r.Arguments.Contains("--model")
                && r.Arguments.Contains("opus")
                && r.Arguments.Contains("--effort")
                && r.Arguments.Contains("high")
                // Neither the rules block nor the user task ride the spawn argv anymore — rules
                // come in via the adapter's file-backed reference (here the stub's --settings),
                // the task is pasted after spawn via PasteFileAsSubmittedPromptAsync.
                && r.Arguments.Contains("--settings")
                && r.Arguments.Contains(SettingsPath)
                && !r.Arguments.Contains("TASK")
                && r.WorkingDirectory.EndsWith($"intents/{IntentIdValue}")),
            Arg.Any<CancellationToken>());
        await fixture.Tmux.Received(1).PasteFileAsSubmittedPromptAsync(
            IntentIdValue,
            Arg.Is<string>(p => p.EndsWith($"intents/{IntentIdValue}/throne-session.user-prompt.txt")),
            Arg.Any<CancellationToken>());
        await fixture.Intents.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.Work,
            appendText: null,
            reason: null,
            IntentTrainingAuthor.System,
            "terminal:spawn:work",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Free-режим с пустой задачей спавнит claude и не paste-ит ничего в pane")]
    public async Task Run_free_mode_empty_task_boots_bare()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult(TmuxSessionName.For(IntentIdValue), IsAlive: true, Detail: null));

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Free, DefaultLaunch, NoPrompt, restart: false, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Running);
        await fixture.Tmux.Received(1).SpawnAsync(
            Arg.Is<TmuxSpawnRequest>(r =>
                r.Command == "claude"
                && r.Arguments.SequenceEqual(ClaudeBareArgs)),
            Arg.Any<CancellationToken>());
        await fixture.Tmux.DidNotReceive().PasteFileAsSubmittedPromptAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.Tmux.DidNotReceive().SendLiteralTextAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await fixture.Intents.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.Work,
            appendText: null,
            reason: null,
            IntentTrainingAuthor.System,
            "terminal:spawn:free",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Большой UserPrompt не валит спавн — задача доставляется paste-ом из файла")]
    public async Task Run_large_user_prompt_delivered_via_paste()
    {
        var hugePrompt = new string('Я', 25_000);
        var bigPrompt = new TerminalSpawnPrompt(SystemPrompt: "RULES", UserPrompt: hugePrompt, null, null);
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult(TmuxSessionName.For(IntentIdValue), IsAlive: true, Detail: null));

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Work, DefaultLaunch, bigPrompt, restart: false, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Running);
        await fixture.Tmux.Received(1).SpawnAsync(
            Arg.Is<TmuxSpawnRequest>(r => r.Arguments.All(a => a.Length < 1000)),
            Arg.Any<CancellationToken>());
        await fixture.Tmux.Received(1).PasteFileAsSubmittedPromptAsync(
            IntentIdValue,
            Arg.Is<string>(p => p.EndsWith("throne-session.user-prompt.txt")),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Run с broken-binding возвращает blocked и не спавнит tmux")]
    public async Task Run_blocked_when_binding_broken()
    {
        var broken = NewBinding(cloneStatus: CloneStatusNames.Broken);
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [broken]);

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Work, DefaultLaunch, NoPrompt, restart: false, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Blocked);
        result.BlockingBindings.Should().ContainSingle().Which.Should().Be(broken.Id.Value);
        await fixture.Tmux.DidNotReceive().SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Run при живой сессии без restart=true бросает terminal.session_already_running")]
    public async Task Run_existing_session_throws_409()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, DefaultLaunch, NoPrompt, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalSessionAlreadyRunning);
    }

    [Fact(DisplayName = "Restart=true сначала kill-session, затем спавнит заново")]
    public async Task Restart_kills_then_spawns()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: true,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult(TmuxSessionName.For(IntentIdValue), IsAlive: true, Detail: null));
        fixture.Tmux.KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>()).Returns(true);

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Interview, DefaultLaunch, CuratedPrompt, restart: true, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Running);
        Received.InOrder(() =>
        {
            fixture.Tmux.KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>());
            fixture.Tmux.SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Run на отсутствующий intent → intent.not_found")]
    public async Task Run_missing_intent_throws()
    {
        var fixture = new Fixture().Setup(capabilityEnabled: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, DefaultLaunch, NoPrompt, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "Spawn вернул IsAlive=false → terminal.spawn_failed")]
    public async Task Run_spawn_not_alive_throws()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult($"throne-{IntentIdValue}", IsAlive: false, Detail: "tmux: command not found"));

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, DefaultLaunch, CuratedPrompt, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalSpawnFailed);
        await fixture.Intents.DidNotReceive().SetStatusAsync(
            Arg.Any<IntentId>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<IntentTrainingAuthor>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

}
