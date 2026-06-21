using System.Text;
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

public class RunPreflightSpawnStagingTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Спавн дампит байты вложений в воркспейс и сносит стейл-стейджинг прошлого запуска")]
    public async Task Spawn_dumps_attachments_and_resets_stale_staging()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"throne-staging-{Guid.NewGuid():N}");
        var workspacePath = Path.Combine(workspaceRoot, "intents", "intent-1");

        // Stale artifacts from a prior run that must not survive the spawn.
        SeedFile(workspacePath, ".claude/skills/throne-review-artifact/SKILL.md", "stale generated review skill");
        SeedFile(workspacePath, ".claude/skills/review/SKILL.md", "stale review skill");
        SeedFile(workspacePath, "bin/throne-review", "stale review script");
        SeedFile(workspacePath, ".throne/attachments/removed-upstream.txt", "gone");
        // A non-Throne skill the operator owns — must be left intact.
        SeedFile(workspacePath, ".claude/skills/keep-me/SKILL.md", "operator skill");

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        var image = new IntentAttachment("att-9", "intent-1", "diagram.png", "image/png", 4, Now);
        attachments.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentAttachment>>([image]));
        var bytes = new byte[] { 1, 2, 3, 4 };
        attachments.OpenContentAsync(Arg.Any<IntentId>(), "att-9", Arg.Any<CancellationToken>())
            .Returns(_ => new IntentAttachmentContent(image, new MemoryStream(bytes)));

        var sut = BuildSpawn(workspaceRoot, attachments);

        try
        {
            await sut.SpawnAsync(
                new IntentId("intent-1"),
                "throne-intent-1",
                TerminalRunModes.Work,
                new TerminalLaunchOptions(TerminalAgentCatalog.VendorClaude, "sonnet", Effort: null),
                new TerminalSpawnPrompt("RULES", "TASK", null, null),
                skillPackages: [],
                CancellationToken.None);

            var dumped = Path.Combine(workspacePath, ".throne", "attachments", "att-9-diagram.png");
            File.Exists(dumped).Should().BeTrue();
            (await File.ReadAllBytesAsync(dumped)).Should().Equal(bytes);

            File.Exists(Path.Combine(workspacePath, ".throne", "attachments", "removed-upstream.txt"))
                .Should().BeFalse();
            Directory.Exists(Path.Combine(workspacePath, ".claude", "skills", "review"))
                .Should().BeFalse();
            Directory.Exists(Path.Combine(workspacePath, ".claude", "skills", "throne-review-artifact"))
                .Should().BeFalse();
            File.Exists(Path.Combine(workspacePath, "bin", "throne-review"))
                .Should().BeFalse();
            File.Exists(Path.Combine(workspacePath, ".claude", "skills", "keep-me", "SKILL.md"))
                .Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    private static void SeedFile(string workspacePath, string relativePath, string content)
    {
        var path = Path.Combine(workspacePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static RunPreflightSpawn BuildSpawn(string workspaceRoot, IIntentAttachmentRepository attachments)
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TmuxSpawnResult("throne-intent-1", IsAlive: true, Detail: null));
        tmux.CapturePaneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("│ > ready");

        var options = new RunPreflightOptions
        {
            TuiReadinessTimeoutMilliseconds = 200,
            TuiReadinessPollIntervalMilliseconds = 20,
        };
        var intents = Substitute.For<IIntentRepository>();
        intents.SetStatusAsync(
                Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => new SetIntentStatusOutcome.Updated(
                Intent.Restore(ci.ArgAt<IntentId>(0), "x", ci.ArgAt<string>(1), 1, [], Now, Now)));

        var preparer = new RunPreflightWorkspacePreparer(
            Substitute.For<IWorkspaceTrust>(), new WorkspaceAttachmentDumper(attachments));
        return new RunPreflightSpawn(
            tmux,
            new WorkspaceRoot(workspaceRoot),
            preparer,
            [new StubAdapter()],
            new TmuxTuiReadinessWaiter(
                tmux, options, TimeProvider.System, new TerminalReadinessSignals(),
                NullLogger<TmuxTuiReadinessWaiter>.Instance),
            options,
            new SetIntentStatusHandler(intents, new PassthroughUnitOfWork(), TimeProvider.System),
            Substitute.For<IDomainEventDispatcher>());
    }

    private sealed class StubAdapter : ISessionHookAdapter
    {
        public string Vendor => TerminalAgentCatalog.VendorClaude;

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId, string workspacePath, string mode, string? systemPrompt,
            IReadOnlyList<SessionSkillPackage> skillPackages, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;

        public bool IsTuiReady(string paneSnapshot) =>
            paneSnapshot.Contains("│ >", StringComparison.Ordinal);
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
