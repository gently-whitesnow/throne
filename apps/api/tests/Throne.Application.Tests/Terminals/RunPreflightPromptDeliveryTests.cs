using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.Tags;

namespace Throne.Application.Tests.Terminals;

public class RunPreflightPromptDeliveryTests
{
    private const string IntentId = "intent-delivery-1";
    private const string Vendor = "stub";
    private const string ReadyGlyph = "❯ ready composer";
    private const string WorkingFooter = "✻ Tinkering… esc to interrupt";
    // A prompt long enough to stay visible in the captured pane (≥12-char line) so the confirmer's
    // "composer cleared" heuristic cannot falsely confirm — the only confirm paths are footer/hook.
    private const string StuckPrompt = "please run this distinctive long task line";

    [Fact(DisplayName = "Ready composer + working footer → paste один раз, без unconfirmed-события")]
    public async Task Ready_pane_and_working_footer_pastes_without_unconfirmed_event()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        // First capture resolves readiness (glyph); subsequent captures show the working footer so
        // the confirmer acknowledges the submit.
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(ReadyGlyph, WorkingFooter);
        var events = Substitute.For<IDomainEventDispatcher>();
        var (sut, workspace) = NewDelivery(tmux, events);

        try
        {
            await sut.DeliverAsync(NewRequest(workspace, new EscToInterruptAdapter()), CancellationToken.None);

            await tmux.Received(1).PasteFileAsSubmittedPromptAsync(
                IntentId,
                Arg.Is<string>(p => p.EndsWith("throne-session.user-prompt.txt")),
                Arg.Any<CancellationToken>());
            await events.DidNotReceive().DispatchAsync(
                Arg.Any<TerminalPromptSubmitUnconfirmed>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            CleanUp(workspace);
        }
    }

    [Fact(DisplayName = "Composer так и не нарисовался → нет paste, dispatch unconfirmed")]
    public async Task Composer_never_renders_skips_paste_and_dispatches_unconfirmed()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        // Pane never paints a ready composer — neither glyph nor screen-stability fires, so the
        // readiness gate times out before any paste.
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        var events = Substitute.For<IDomainEventDispatcher>();
        var (sut, workspace) = NewDelivery(tmux, events);

        try
        {
            await sut.DeliverAsync(NewRequest(workspace, new EscToInterruptAdapter()), CancellationToken.None);

            await tmux.DidNotReceiveWithAnyArgs().PasteFileAsSubmittedPromptAsync(default!, default!, default);
            await events.Received(1).DispatchAsync(
                Arg.Any<TerminalPromptSubmitUnconfirmed>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            CleanUp(workspace);
        }
    }

    [Fact(DisplayName = "Ready composer, но footer и hook так и не пришли → paste один раз, Enter переслан, dispatch unconfirmed")]
    public async Task Ready_composer_but_submit_never_acknowledged_dispatches_unconfirmed()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        // Readiness resolves on the first capture (glyph); every later capture stays the ready glyph
        // with the pasted prompt still sitting unsubmitted in the composer — no working footer and no
        // hook, so the confirmer never sees the submit and exhausts its one retry (re-sending Enter).
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns($"{ReadyGlyph}\n{StuckPrompt}");
        var events = Substitute.For<IDomainEventDispatcher>();
        var (sut, workspace) = NewDelivery(tmux, events);

        try
        {
            await sut.DeliverAsync(
                NewRequest(workspace, new EscToInterruptAdapter(), userPrompt: StuckPrompt),
                CancellationToken.None);

            await tmux.Received(1).PasteFileAsSubmittedPromptAsync(
                IntentId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            await tmux.Received(1).SendEnterAsync(IntentId, Arg.Any<CancellationToken>());
            await events.Received(1).DispatchAsync(
                Arg.Any<TerminalPromptSubmitUnconfirmed>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            CleanUp(workspace);
        }
    }

    [Fact(DisplayName = "Adapter == null (неизвестный vendor) → paste один раз, без события")]
    public async Task Null_adapter_pastes_best_effort_without_event()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        var events = Substitute.For<IDomainEventDispatcher>();
        var (sut, workspace) = NewDelivery(tmux, events);

        try
        {
            await sut.DeliverAsync(NewRequest(workspace, adapter: null), CancellationToken.None);

            await tmux.Received(1).PasteFileAsSubmittedPromptAsync(
                IntentId, Arg.Any<string>(), Arg.Any<CancellationToken>());
            await tmux.DidNotReceiveWithAnyArgs().CapturePaneAsync(default!, default);
            await events.DidNotReceive().DispatchAsync(
                Arg.Any<TerminalPromptSubmitUnconfirmed>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            CleanUp(workspace);
        }
    }

    [Fact(DisplayName = "Карта workspace (root + пути репо + теги + связи) допишется в начало задачи перед paste")]
    public async Task Prepends_workspace_map_with_repo_paths_above_the_task()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        var events = Substitute.For<IDomainEventDispatcher>();
        var throne = TagId.New();
        var must = TagId.New();
        var tagRepo = Substitute.For<ITagRepository>();
        tagRepo.GetByIdAsync(throne, Arg.Any<CancellationToken>())
            .Returns(Tag.Create(throne, "throne", DateTimeOffset.UnixEpoch));
        tagRepo.GetByIdAsync(must, Arg.Any<CancellationToken>())
            .Returns(Tag.Create(must, "must", DateTimeOffset.UnixEpoch));
        var linkRepo = Substitute.For<IIntentLinkRepository>();
        linkRepo.ListByIntentAsync(new IntentId(IntentId), Arg.Any<CancellationToken>())
            .Returns([
                LinkView("blocked-by-id", blocking: true, incoming: true, rationale: null),
                LinkView("soft-id", blocking: false, incoming: false, rationale: "передать результат дальше"),
            ]);
        var (sut, workspace) = NewDelivery(tmux, events, tagRepo, linkRepo);
        var repo = Path.Combine(workspace, "octo__widget");

        try
        {
            await sut.DeliverAsync(
                NewRequest(
                    workspace, adapter: null, userPrompt: "do the thing",
                    repoPaths: [repo], tagIds: [throne, must]),
                CancellationToken.None);

            var delivered = await File.ReadAllTextAsync(
                Path.Combine(workspace, "throne-session.user-prompt.txt"));
            delivered.Should().Contain("Карта workspace");
            delivered.Should().Contain(workspace);
            delivered.Should().Contain(repo);
            delivered.Should().Contain("Теги интента: throne, must");
            delivered.Should().Contain("Связи:");
            delivered.Should().Contain("- заблокирован intent_id=blocked-by-id status=work (без причины связи)");
            delivered.Should().Contain("- ведёт к intent_id=soft-id status=work: передать результат дальше");
            delivered.Should().Contain("не угадывай имя клон-сабдира");
            delivered.Should().Contain("cwd между Bash-вызовами не гарантирована");
            // Map sits above the original task, not appended after it.
            delivered.IndexOf("Карта workspace", StringComparison.Ordinal)
                .Should().BeLessThan(delivered.IndexOf("do the thing", StringComparison.Ordinal));
        }
        finally
        {
            CleanUp(workspace);
        }
    }

    private static (RunPreflightPromptDelivery Delivery, string Workspace) NewDelivery(
        ITmuxSessionManager tmux,
        IDomainEventDispatcher events,
        ITagRepository? tagRepo = null,
        IIntentLinkRepository? linkRepo = null)
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"throne-delivery-{Guid.NewGuid():N}");
        var options = new RunPreflightOptions
        {
            TuiReadinessTimeoutMilliseconds = 150,
            TuiReadinessPollIntervalMilliseconds = 20,
            PromptSubmitConfirmTimeoutMilliseconds = 150,
            PromptSubmitMaxRetries = 1,
        };
        var readinessWaiter = new TmuxTuiReadinessWaiter(
            tmux, options, TimeProvider.System, new TerminalReadinessSignals(),
            NullLogger<TmuxTuiReadinessWaiter>.Instance);
        var confirmer = new TmuxPromptSubmitConfirmer(
            tmux, options, TimeProvider.System, NullLogger<TmuxPromptSubmitConfirmer>.Instance);
        var delivery = new RunPreflightPromptDelivery(
            tmux, readinessWaiter, confirmer, new TerminalPromptSubmitSignals(), events,
            new RunPreflightTagNames(tagRepo ?? Substitute.For<ITagRepository>()),
            new IntentLinkPromptContextReader(linkRepo ?? EmptyLinks()),
            NullLogger<RunPreflightPromptDelivery>.Instance);
        return (delivery, workspace);
    }

    private static TerminalPromptDeliveryRequest NewRequest(
        string workspace,
        ISessionHookAdapter? adapter,
        string userPrompt = "TASK",
        IReadOnlyList<string>? repoPaths = null,
        IReadOnlyList<TagId>? tagIds = null) =>
        new(
            IntentId, TerminalRunModes.Work, Vendor, adapter, workspace,
            repoPaths ?? [], tagIds ?? [], userPrompt);

    private static IIntentLinkRepository EmptyLinks()
    {
        var repo = Substitute.For<IIntentLinkRepository>();
        repo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns([]);
        return repo;
    }

    private static IntentLinkView LinkView(string peerId, bool blocking, bool incoming, string? rationale)
    {
        var intentId = new IntentId(IntentId);
        var peer = Intent.Restore(new IntentId(peerId), $"{peerId} body", IntentStatusNames.Work, 1, [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var link = IntentLink.Create(
            $"link-{peerId}",
            incoming ? peer.Id : intentId,
            incoming ? intentId : peer.Id,
            blocking,
            IntentLinkAuthor.User,
            rationale,
            DateTimeOffset.UnixEpoch);
        return new IntentLinkView(
            link,
            incoming ? IntentLinkDirection.Incoming : IntentLinkDirection.Outgoing,
            peer);
    }

    private static void CleanUp(string workspace)
    {
        if (Directory.Exists(workspace))
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private sealed class EscToInterruptAdapter : ISessionHookAdapter
    {
        public string Vendor => "stub";

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId, string workspacePath, string mode, string? systemPrompt,
            IReadOnlyList<SessionSkillPackage> skillPackages, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;

        public bool IsTuiReady(string paneSnapshot) =>
            !string.IsNullOrEmpty(paneSnapshot) && paneSnapshot.Contains('❯');

        public bool IsPromptSubmitted(string paneSnapshot) =>
            !string.IsNullOrEmpty(paneSnapshot)
            && paneSnapshot.Contains("esc to interrupt", StringComparison.OrdinalIgnoreCase);
    }
}
