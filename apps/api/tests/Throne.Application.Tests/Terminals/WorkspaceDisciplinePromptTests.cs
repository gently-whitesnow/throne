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

public class WorkspaceDisciplinePromptTests
{
    [Fact(DisplayName = "Work-режим: системный промпт префиксуется workspace-discipline-параграфом с путём и intent id")]
    public async Task Work_mode_prepends_discipline_paragraph_with_workspace_path_and_intent_id()
    {
        var adapter = new CapturingAdapter();
        var (sut, workspaceRoot, intentId) = BuildSpawn(adapter);

        try
        {
            await sut.SpawnAsync(
                new IntentId(intentId),
                "throne-intent-x",
                TerminalRunModes.Work,
                new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "sonnet", Effort: null),
                new TerminalSpawnPrompt("CURATED-RULES", UserPrompt: null, null, null),
                skillPackages: [],
                CancellationToken.None);

            var expectedWorkspace = Path.Combine(workspaceRoot, "intents", intentId);
            adapter.SystemPrompt.Should().NotBeNull();
            adapter.SystemPrompt!.Should().StartWith("Твой intent workspace: `" + expectedWorkspace + "`");
            adapter.SystemPrompt.Should().Contain("intent id: `" + intentId + "`");
            adapter.SystemPrompt.Should().EndWith("CURATED-RULES");
        }
        finally
        {
            CleanupWorkspace(workspaceRoot);
        }
    }

    [Fact(DisplayName = "Work-режим без курируемых частей: параграф уходит в адаптер как самостоятельный системный промпт")]
    public async Task Work_mode_emits_paragraph_when_curated_prompt_is_empty()
    {
        var adapter = new CapturingAdapter();
        var (sut, workspaceRoot, intentId) = BuildSpawn(adapter);

        try
        {
            await sut.SpawnAsync(
                new IntentId(intentId),
                "throne-intent-x",
                TerminalRunModes.Work,
                new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "sonnet", Effort: null),
                new TerminalSpawnPrompt(SystemPrompt: null, UserPrompt: null, null, null),
                skillPackages: [],
                CancellationToken.None);

            adapter.SystemPrompt.Should().NotBeNull();
            adapter.SystemPrompt!.Should().StartWith("Твой intent workspace:");
        }
        finally
        {
            CleanupWorkspace(workspaceRoot);
        }
    }

    [Theory(DisplayName = "Не-Work режимы: системный промпт уходит в адаптер без правок")]
    [InlineData(TerminalRunModes.Interview)]
    [InlineData(TerminalRunModes.Review)]
    [InlineData(TerminalRunModes.Free)]
    [InlineData(TerminalRunModes.Dream)]
    public async Task Non_work_modes_pass_curated_prompt_through_unchanged(string mode)
    {
        var adapter = new CapturingAdapter();
        var (sut, workspaceRoot, intentId) = BuildSpawn(adapter);

        try
        {
            await sut.SpawnAsync(
                new IntentId(intentId),
                "throne-intent-x",
                mode,
                new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "sonnet", Effort: null),
                new TerminalSpawnPrompt("CURATED-RULES", UserPrompt: null, null, null),
                skillPackages: [],
                CancellationToken.None);

            adapter.SystemPrompt.Should().Be("CURATED-RULES");
        }
        finally
        {
            CleanupWorkspace(workspaceRoot);
        }
    }

    private static (RunPreflightSpawn Spawn, string WorkspaceRoot, string IntentId) BuildSpawn(CapturingAdapter adapter)
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"throne-wsd-{Guid.NewGuid():N}");
        var intentId = "intent-wsd";
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TmuxSpawnResult("throne-intent-x", IsAlive: true, Detail: null));
        tmux.CapturePaneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("│ > ready");
        var options = new RunPreflightOptions
        {
            TuiReadinessTimeoutMilliseconds = 100,
            TuiReadinessPollIntervalMilliseconds = 20,
        };
        var intents = Substitute.For<IIntentRepository>();
        intents.SetStatusAsync(
                Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => new SetIntentStatusOutcome.Updated(
                Intent.Restore(ci.ArgAt<IntentId>(0), "x", ci.ArgAt<string>(1), 1, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)));
        var spawn = new RunPreflightSpawn(
            tmux,
            new WorkspaceRoot(workspaceRoot),
            TerminalSpawnTestDoubles.EmptyWorkspacePreparer(),
            [adapter],
            new TmuxTuiReadinessWaiter(
                tmux, options, TimeProvider.System, new TerminalReadinessSignals(),
                NullLogger<TmuxTuiReadinessWaiter>.Instance),
            options,
            new SetIntentStatusHandler(intents, new PassthroughUnitOfWork(), TimeProvider.System),
            Substitute.For<IDomainEventDispatcher>());
        return (spawn, workspaceRoot, intentId);
    }

    private static void CleanupWorkspace(string workspaceRoot)
    {
        if (Directory.Exists(workspaceRoot))
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private sealed class CapturingAdapter : ISessionHookAdapter
    {
        public string? SystemPrompt { get; private set; }
        public string Vendor => TerminalAgentCatalog.VendorClaude;

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId, string workspacePath, string mode, string? systemPrompt,
            IReadOnlyList<SessionSkillPackage> skillPackages, CancellationToken ct)
        {
            SystemPrompt = systemPrompt;
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;

        public bool IsTuiReady(string paneSnapshot) => paneSnapshot.Contains("│ >", StringComparison.Ordinal);
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
