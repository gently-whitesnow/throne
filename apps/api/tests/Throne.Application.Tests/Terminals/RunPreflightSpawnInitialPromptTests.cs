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
    [Fact(DisplayName = "OpenCode доставляет первый prompt через native submit без capture-pane и tmux paste")]
    public async Task Opencode_initial_prompt_uses_native_submitter()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"throne-spawn-{Guid.NewGuid():N}");
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>())
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

            adapter.Submitted.Should().Equal([("intent-1", Path.Combine(workspaceRoot, "intents", "intent-1"), "TASK")]);
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

    private sealed class NativeAdapter : ISessionHookAdapter, INativeInitialPromptSubmitter
    {
        public string Vendor => TerminalAgentCatalog.VendorOpencode;
        public List<(string IntentId, string WorkspacePath, string Prompt)> Submitted { get; } = [];

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId,
            string workspacePath,
            string mode,
            string? systemPrompt,
            ReviewArtifactWriteTarget? reviewArtifact,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(["--hostname", "127.0.0.1", "--port", "49152"]);

        public Task SubmitInitialPromptAsync(
            string intentId,
            string workspacePath,
            string userPrompt,
            CancellationToken ct)
        {
            Submitted.Add((intentId, workspacePath, userPrompt));
            return Task.CompletedTask;
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
