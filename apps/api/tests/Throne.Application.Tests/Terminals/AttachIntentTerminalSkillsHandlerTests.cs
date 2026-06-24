using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
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
        ex.Which.Code.Should().Be(TerminalErrorCodes.SessionNotLive);
        await fixture.Tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
        await fixture.Launches.DidNotReceiveWithAnyArgs().SetAttachedSkillIdsAsync(default!, default!, default!, default);
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
        ex.Which.Code.Should().Be(TerminalErrorCodes.SessionSkillUnknown);
        await fixture.Tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Vendor=codex hot-attach пишет native .agents skill и persist-ит set")]
    public async Task Attach_codex_vendor_materializes_native_skill_and_persists()
    {
        var codexLaunch = new TerminalLaunchRecord(
            TerminalRunModes.Work, TerminalAgentCatalog.VendorCodex, "gpt-5.5", "high",
            Array.Empty<string>(), EmptySelections);
        var fixture = new Fixture().Setup(intentExists: true, hasSession: true, launch: codexLaunch);

        var result = await fixture.Handler.HandleAsync(
            new AttachIntentTerminalSkillsRequest(IntentIdValue, [SessionSkillPackageIds.Intent]),
            CancellationToken.None);

        result.AttachedSkillIds.Should().Equal(SessionSkillPackageIds.Intent);
        var workspacePath = Path.Combine(WorkspaceRoot, "intents", IntentIdValue);
        File.Exists(Path.Combine(workspacePath, ".agents", "skills", SessionSkillPackageIds.Intent, "SKILL.md"))
            .Should().BeTrue();
        await fixture.Tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Vendor=opencode → SessionSkillVendorUnsupported 422")]
    public async Task Attach_opencode_vendor_throws_422()
    {
        var opencodeLaunch = new TerminalLaunchRecord(
            TerminalRunModes.Work, TerminalAgentCatalog.VendorOpencode, "llama-4", null,
            Array.Empty<string>(), EmptySelections);
        var fixture = new Fixture().Setup(intentExists: true, hasSession: true, launch: opencodeLaunch);

        var act = () => fixture.Handler.HandleAsync(
            new AttachIntentTerminalSkillsRequest(IntentIdValue, [SessionSkillPackageIds.Intent]),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(TerminalErrorCodes.SessionSkillVendorUnsupported);
    }

    [Fact(DisplayName = "Happy path: скилы материализуются без paste, union persist-ится")]
    public async Task Attach_happy_path_writes_skills_without_paste_and_persists_union()
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

        var canonical = await File.ReadAllTextAsync(
            Path.Combine(workspacePath, "skills", SessionSkillPackageIds.Intent, "SKILL.md"));
        canonical.Should().Contain("# intent skill stub");
        var pointer = await File.ReadAllTextAsync(
            Path.Combine(workspacePath, ".claude", "skills", SessionSkillPackageIds.Intent, "SKILL.md"));
        pointer.Should().Contain("skills/intent/SKILL.md");
        pointer.Should().NotContain("# intent skill stub");
        var scriptPath = Path.Combine(
            workspacePath, "skills", SessionSkillPackageIds.Intent, "bin", "throne-intent");
        File.Exists(scriptPath).Should().BeTrue();
        AssertExecutable(scriptPath);

        await fixture.Tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
        await fixture.Launches.Received(1).SetAttachedSkillIdsAsync(
            IntentIdValue,
            TerminalRunModes.Work,
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
            TerminalRunModes.Work,
            Arg.Is<IReadOnlyList<string>>(ids =>
                ids.Count == 1 && ids[0] == SessionSkillPackageIds.Intent),
            Arg.Any<CancellationToken>());
        await fixture.Tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptySelections =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    private static TerminalLaunchRecord ClaudeLaunch(IReadOnlyList<string>? previousAttached = null) =>
        new(TerminalRunModes.Work,
            TerminalAgentCatalog.VendorClaude,
            "opus",
            "high",
            previousAttached ?? Array.Empty<string>(),
            EmptySelections);

    private sealed class Fixture
    {
        public IIntentRepository Intents { get; } = Substitute.For<IIntentRepository>();
        public IIntentRepositoryBindingRepository Bindings { get; } = Substitute.For<IIntentRepositoryBindingRepository>();
        public IIntentTerminalLaunchStore Launches { get; } = Substitute.For<IIntentTerminalLaunchStore>();
        public ISessionSkillCatalog Catalog { get; } = TerminalSpawnTestDoubles.SkillCatalog();
        public ITmuxSessionManager Tmux { get; } = Substitute.For<ITmuxSessionManager>();
        public ISessionSkillHotAttachWriter Writer { get; } = new TestSkillWriter();

        public AttachIntentTerminalSkillsHandler Handler { get; private set; } = default!;

        public Fixture Setup(
            bool intentExists,
            bool hasSession = false,
            TerminalLaunchRecord? launch = null)
        {
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
                Substitute.For<ISkillModeDefaultStore>());
            Handler = new AttachIntentTerminalSkillsHandler(
                Intents, Bindings, Launches, Catalog, TerminalSpawnTestDoubles.VendorCatalog(),
                selection, Tmux, Writer);
            return this;
        }
    }

    private sealed class TestSkillWriter : ISessionSkillHotAttachWriter
    {
        public Task<HotAttachMaterialization> MaterializeAsync(
            SessionSkillPackageResolution resolution,
            CancellationToken ct)
        {
            // Mirror infrastructure side-effect: create the SKILL.md + bin files so the happy-path test
            // can assert filesystem layout without pulling Throne.Infrastructure into the unit
            // suite (which would also require the static skill source tree at AppContext.BaseDirectory).
            var workspacePath = Path.Combine(WorkspaceRoot, "intents", resolution.IntentId);
            foreach (var skillId in resolution.SelectedSkillIds)
            {
                var canonical = Path.Combine(workspacePath, "skills", skillId, "SKILL.md");
                Directory.CreateDirectory(Path.GetDirectoryName(canonical)!);
                File.WriteAllText(canonical, $"# {skillId} skill stub\n");
                var vendorRoot = resolution.Vendor == TerminalAgentCatalog.VendorCodex
                    ? ".agents"
                    : ".claude";
                var pointer = Path.Combine(workspacePath, vendorRoot, "skills", skillId, "SKILL.md");
                Directory.CreateDirectory(Path.GetDirectoryName(pointer)!);
                File.WriteAllText(pointer, $"Read skills/{skillId}/SKILL.md\n");
                var script = Path.Combine(workspacePath, "skills", skillId, "bin", $"throne-{skillId}");
                Directory.CreateDirectory(Path.GetDirectoryName(script)!);
                File.WriteAllText(script, "#!/usr/bin/env sh\n");
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(
                        script,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
            }
            return Task.FromResult(new HotAttachMaterialization(workspacePath));
        }
    }

    private static void AssertExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.GetUnixFileMode(path).Should().HaveFlag(UnixFileMode.UserExecute);
    }
}
