using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Terminals;

public class AttachIntentTerminalSkillsHandlerTests
{
    private const string IntentIdValue = "intent-attach-1";
    private const string WorkspaceRoot = "/tmp/throne-attach-tests";

    [Fact(DisplayName = "Attach без живой сессии → TerminalSessionNotLive 409")]
    public async Task Attach_without_live_session_throws_409()
    {
        var fixture = new Fixture().Setup(intentExists: true, hasSession: false);

        var act = () => fixture.Handler.HandleAsync(
            new AttachIntentTerminalSkillsRequest(IntentIdValue, [SessionSkillPackageIds.Intent]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalSessionNotLive);
        await fixture.Tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
        await fixture.Launches.DidNotReceiveWithAnyArgs().SetAttachedSkillIdsAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Unknown skill id → SessionSkillUnknown 422")]
    public async Task Attach_unknown_skill_throws_422()
    {
        var fixture = new Fixture().Setup(intentExists: true, hasSession: true,
            launch: ClaudeLaunch());

        var act = () => fixture.Handler.HandleAsync(
            new AttachIntentTerminalSkillsRequest(IntentIdValue, ["totally-not-a-skill"]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.SessionSkillUnknown);
        await fixture.Tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Vendor=codex → SessionSkillVendorUnsupported 422")]
    public async Task Attach_non_claude_vendor_throws_422()
    {
        var codexLaunch = new TerminalLaunchRecord(
            TerminalRunModes.Work, TerminalAgentCatalog.VendorCodex, "gpt-5.5", "high", Array.Empty<string>());
        var fixture = new Fixture().Setup(intentExists: true, hasSession: true, launch: codexLaunch);

        var act = () => fixture.Handler.HandleAsync(
            new AttachIntentTerminalSkillsRequest(IntentIdValue, [SessionSkillPackageIds.Intent]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.SessionSkillVendorUnsupported);
    }

    [Fact(DisplayName = "Happy path: два intent-скила записываются, paste вызывается один раз, union persist-ится")]
    public async Task Attach_happy_path_writes_skills_pastes_reminder_and_persists_union()
    {
        var workspacePath = Path.Combine(WorkspaceRoot, "intents", IntentIdValue);
        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }
        Directory.CreateDirectory(workspacePath);
        var launch = ClaudeLaunch(previousAttached: [SessionSkillPackageIds.Dream]);
        var fixture = new Fixture().Setup(intentExists: true, hasSession: true, launch: launch);

        var result = await fixture.Handler.HandleAsync(
            new AttachIntentTerminalSkillsRequest(IntentIdValue,
                [SessionSkillPackageIds.Intent, SessionSkillPackageIds.Dream]),
            CancellationToken.None);

        // Union = previous {dream} ∪ requested {intent, dream}
        result.AttachedSkillIds.Should().BeEquivalentTo(
            new[] { SessionSkillPackageIds.Dream, SessionSkillPackageIds.Intent });

        // SKILL.md files were materialized into .claude/skills/{id}/SKILL.md
        File.Exists(Path.Combine(workspacePath, ".claude", "skills", SessionSkillPackageIds.Intent, "SKILL.md"))
            .Should().BeTrue();

        await fixture.Tmux.Received(1).PasteFileAsSubmittedPromptAsync(
            IntentIdValue,
            Arg.Is<string>(p => p.Contains("throne-session.skill-attach.", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await fixture.Launches.Received(1).SetAttachedSkillIdsAsync(
            IntentIdValue,
            Arg.Is<IReadOnlyList<string>>(ids =>
                ids.Count == 2
                && ids.Contains(SessionSkillPackageIds.Intent)
                && ids.Contains(SessionSkillPackageIds.Dream)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Idempotent: повторный вызов с теми же id не дублирует persisted set")]
    public async Task Attach_is_idempotent_for_persisted_set()
    {
        var workspacePath = Path.Combine(WorkspaceRoot, "intents", IntentIdValue);
        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, recursive: true);
        }
        Directory.CreateDirectory(workspacePath);
        var launch = ClaudeLaunch(previousAttached: [SessionSkillPackageIds.Intent]);
        var fixture = new Fixture().Setup(intentExists: true, hasSession: true, launch: launch);

        var result = await fixture.Handler.HandleAsync(
            new AttachIntentTerminalSkillsRequest(IntentIdValue, [SessionSkillPackageIds.Intent]),
            CancellationToken.None);

        result.AttachedSkillIds.Should().BeEquivalentTo(new[] { SessionSkillPackageIds.Intent });
        await fixture.Launches.Received(1).SetAttachedSkillIdsAsync(
            IntentIdValue,
            Arg.Is<IReadOnlyList<string>>(ids =>
                ids.Count == 1 && ids[0] == SessionSkillPackageIds.Intent),
            Arg.Any<CancellationToken>());
        // Reminder is still re-injected on every call so the live agent always sees the latest intent.
        await fixture.Tmux.Received(1).PasteFileAsSubmittedPromptAsync(
            IntentIdValue, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static TerminalLaunchRecord ClaudeLaunch(IReadOnlyList<string>? previousAttached = null) =>
        new(TerminalRunModes.Work,
            TerminalAgentCatalog.VendorClaude,
            "opus",
            "high",
            previousAttached ?? Array.Empty<string>());

    private sealed class Fixture
    {
        public IIntentRepository Intents { get; } = Substitute.For<IIntentRepository>();
        public IIntentRepositoryBindingRepository Bindings { get; } = Substitute.For<IIntentRepositoryBindingRepository>();
        public IIntentTerminalLaunchStore Launches { get; } = Substitute.For<IIntentTerminalLaunchStore>();
        public ISessionSkillCatalog Catalog { get; } = new InMemorySessionSkillCatalog();
        public ITmuxSessionManager Tmux { get; } = Substitute.For<ITmuxSessionManager>();
        public IWorkspaceRootProvider WorkspaceRoot { get; } = Substitute.For<IWorkspaceRootProvider>();
        public ISessionSkillHotAttachWriter Writer { get; } = new TestSkillWriter();

        public AttachIntentTerminalSkillsHandler Handler { get; private set; } = default!;

        public Fixture Setup(
            bool intentExists,
            bool hasSession = false,
            TerminalLaunchRecord? launch = null)
        {
            WorkspaceRoot.ResolvedRoot.Returns(AttachIntentTerminalSkillsHandlerTests.WorkspaceRoot);

            if (intentExists)
            {
                var intentId = new IntentId(IntentIdValue);
                var intent = Intent.Restore(intentId, "x", IntentStatusNames.Work, 1, [],
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                Intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns(intent);
            }
            Bindings.FindByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<IntentRepositoryBinding>());
            Tmux.HasSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(hasSession);
            Launches.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(launch);

            var selection = new SessionSkillSelectionService(
                Catalog,
                Substitute.For<ISkillModeDefaultStore>(),
                Substitute.For<IIntentSkillModeSelectionStore>());
            var registry = new SessionSkillPackageRegistry(Catalog);
            Handler = new AttachIntentTerminalSkillsHandler(
                Intents, Bindings, Launches, Catalog, selection, registry, Tmux, WorkspaceRoot, Writer);
            return this;
        }
    }

    private sealed class TestSkillWriter : ISessionSkillHotAttachWriter
    {
        public Task<IReadOnlyList<HotAttachedSkillContent>> WriteAsync(
            string workspacePath,
            IReadOnlyList<SessionSkillPackage> packages,
            CancellationToken ct)
        {
            // Mirror infrastructure side-effect: create the SKILL.md files so the happy-path test
            // can assert filesystem layout without pulling Throne.Infrastructure into the unit
            // suite (which would also require the static skill source tree at AppContext.BaseDirectory).
            foreach (var package in packages)
            {
                var target = Path.Combine(workspacePath, ".claude", "skills", package.Id, "SKILL.md");
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, $"# {package.Id} skill stub\n");
            }
            var content = packages
                .Select(p => new HotAttachedSkillContent(p.Id, $"# {p.Id} skill stub\n"))
                .ToArray();
            return Task.FromResult<IReadOnlyList<HotAttachedSkillContent>>(content);
        }
    }
}
