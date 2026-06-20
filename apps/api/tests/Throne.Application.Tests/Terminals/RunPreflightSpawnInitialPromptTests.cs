using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Tests.Terminals;

public class RunPreflightSpawnInitialPromptTests
{
    [Fact(DisplayName = "OpenCode инициализирует сессию до spawn и спавнит attach-argv без capture-pane/paste")]
    public async Task Opencode_initial_prompt_uses_native_session_initializer()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"throne-spawn-{Guid.NewGuid():N}");
        var tmux = Substitute.For<ITmuxSessionManager>();
        TmuxSpawnRequest? spawned = null;
        tmux.SpawnAsync(Arg.Do<TmuxSpawnRequest>(r => spawned = r), Arg.Any<CancellationToken>())
            .Returns(new TmuxSpawnResult("throne-intent-1", IsAlive: true, Detail: null));
        var adapter = new NativeAdapter();
        var intents = Substitute.For<IIntentRepository>();
        intents.SetStatusAsync(
                Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => new SetIntentStatusOutcome.Updated(
                Intent.Restore(ci.ArgAt<IntentId>(0), "x", ci.ArgAt<string>(1), 1, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        var options = new RunPreflightOptions
        {
            TuiReadinessTimeoutMilliseconds = 100,
            TuiReadinessPollIntervalMilliseconds = 20,
        };
        var sut = new RunPreflightSpawn(
            tmux,
            new WorkspaceRoot(workspaceRoot),
            Substitute.For<IWorkspaceTrust>(),
            [adapter],
            new TmuxTuiReadinessWaiter(
                tmux, options, TimeProvider.System, new TerminalReadinessSignals(),
                NullLogger<TmuxTuiReadinessWaiter>.Instance),
            options,
            new SetIntentStatusHandler(intents, new PassthroughUnitOfWork(), TimeProvider.System),
            Substitute.For<IDomainEventDispatcher>());

        try
        {
            await sut.SpawnAsync(
                new IntentId("intent-1"),
                "throne-intent-1",
                TerminalRunModes.Work,
                new TerminalLaunchOptions(TerminalAgentCatalog.VendorOpencode, "qwen", Effort: null),
                new TerminalSpawnPrompt("RULES", "TASK", null, null),
                reviewArtifact: null,
                CancellationToken.None);

            var workspacePath = Path.Combine(workspaceRoot, "intents", "intent-1");
            adapter.Initialized.Should().Equal([("intent-1", workspacePath, "qwen", "TASK")]);
            // The attach argv produced by the initializer is folded into the spawn command.
            spawned!.Arguments.Should().Equal("attach", "http://127.0.0.1:4096", "--session", "ses-1");
            await tmux.DidNotReceive().CapturePaneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            await tmux.DidNotReceive().PasteFileAsSubmittedPromptAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    private sealed class NativeAdapter : ISessionHookAdapter, INativeSessionInitializer
    {
        public string Vendor => TerminalAgentCatalog.VendorOpencode;
        public List<(string IntentId, string WorkspacePath, string Model, string? Prompt)> Initialized { get; } = [];

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId,
            string workspacePath,
            string mode,
            string? systemPrompt,
            IReadOnlyList<SessionSkillPackage> skillPackages,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> InitializeSessionAsync(
            string intentId,
            string workspacePath,
            string model,
            string? userPrompt,
            CancellationToken ct)
        {
            Initialized.Add((intentId, workspacePath, model, userPrompt));
            return Task.FromResult<IReadOnlyList<string>>(
                ["attach", "http://127.0.0.1:4096", "--session", "ses-1"]);
        }

        public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;
        public bool IsTuiReady(string paneSnapshot) => false;
    }

    private sealed class WorkspaceRoot(string root) : IWorkspaceRootProvider
    {
        public string ResolvedRoot { get; } = root;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
