using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Dreams;
using Throne.Application.Events;
using Throne.Application.Ide;
using Throne.Application.Intents;
using Throne.Application.Intents.Events;
using Throne.Application.Intents.Linking;
using Throne.Application.LocalModels;
using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Application.PromptParts;
using Throne.Application.Repositories;
using Throne.Application.Tags;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Application.TextVersions;

namespace Throne.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddThroneApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<TagNameEnsurer>();
        services.AddSingleton<TagIdLookup>();
        services.AddSingleton<IntentTagResolver>();
        services.AddIntentHandlers();
        services.AddSingleton<ListTagsHandler>();
        services.AddSingleton<CreateTagHandler>();
        services.AddSingleton<RenameTagHandler>();
        services.AddSingleton<DeleteTagHandler>();
        // Prompt parts (ADR-0036): single unified entity. The embedded composition reads
        // prompt_parts in manifest-include order.
        services.AddSingleton<ListPromptPartVersionsHandler>();
        services.AddSingleton<CreatePromptPartHandler>();
        services.AddSingleton<ListPromptPartsHandler>();
        services.AddSingleton<GetPromptPartHandler>();
        services.AddSingleton<ReplacePromptPartTextHandler>();
        services.AddSingleton<SetPromptPartRolesHandler>();
        services.AddSingleton<DeletePromptPartHandler>();
        services.AddSingleton<PromptCompositionResolver>();
        services.AddSingleton<IntentTerminalPreviewHandler>();
        // Lazy<IUnitOfWork> breaks the singleton-resolution cycle: the
        // IUnitOfWork factory pulls in IDomainEventDispatcher, which pulls in
        // every IDomainEventHandler — and these two handlers themselves need
        // an IUnitOfWork to commit their cascade. Without Lazy, MS DI would
        // recurse through its own resolution lock and deadlock under load.
        services.AddSingleton(sp => new Lazy<IUnitOfWork>(sp.GetRequiredService<IUnitOfWork>));
        // PromptPartPatch handlers (ADR-0036): patches target a PromptPart
        // by (scope, key); apply / reject is operator-only.
        services.AddSingleton<UserPromptPartLookup>();
        services.AddSingleton<ProposePromptPartPatchHandler>();
        services.AddSingleton<ApplyPromptPartPatchWorkflow>();
        services.AddSingleton<ApplyPromptPartPatchHandler>();
        services.AddSingleton<RejectPromptPartPatchHandler>();
        services.AddSingleton<ListPromptPartPatchesHandler>();
        services.AddSingleton<GetPromptPartPatchHandler>();
        services.AddSingleton<GetCurrentPromptPartHandler>();
        // DreamSession handlers (ADR-0022): the frontier agent reads dialogs
        // locally and records its own memory of each /dream pass through HTTP CLI.
        services.AddSingleton<RecordDreamSessionHandler>();
        services.AddSingleton<ListDreamSessionsHandler>();
        services.AddSingleton<GetDreamSessionHandler>();
        services.AddSingleton<GetDreamSourcesHandler>();
        // Repositories slice: bind/unbind/list + PR sync. Background workers live in
        // Infrastructure; the Application queue/workflow types stay here for unit tests.
        services.AddSingleton<RepositoryBindingResolver>();
        services.AddSingleton<RepositoryBindingPersistence>();
        services.AddSingleton<RepositoryPullRequestSyncPersistence>();
        services.AddSingleton<RepositoryPullRequestSyncWorkflow>();
        services.AddSingleton<RepositoryBindingService>();
        services.AddSingleton<WorkspaceCleanupService>();
        services.AddSingleton<PullRequestArtifactWriter>();
        services.AddSingleton<IPullRequestArtifactSink>(sp => sp.GetRequiredService<PullRequestArtifactWriter>());
        services.AddSingleton<GetPullRequestArtifactHandler>();
        services.AddSingleton<ListPullRequestArtifactsHandler>();
        services.AddSingleton<IIntentRepositoryBindingReader, IntentRepositoryBindingReader>();
        services.AddSingleton<RepositoryCloneRequestsChannel>();
        services.AddSingleton<IRepositoryCloneRequests>(sp => sp.GetRequiredService<RepositoryCloneRequestsChannel>());
        services.AddSingleton<IRepositoryCloneRequestsReader>(sp => sp.GetRequiredService<RepositoryCloneRequestsChannel>());
        services.AddSingleton<RepositoryCloneTransitionWriter>();
        services.AddSingleton<RepositoryCloneWorkflow>();
        services.AddSingleton<RepositoryCloneRecoveryWorkflow>();
        // PR-sync per-tick orchestration; BackgroundService host lives in Infrastructure.
        services.AddSingleton<PullRequestSyncBackoff>();
        services.AddSingleton<IntentMergeAutoCloser>();
        services.AddSingleton<PullRequestStateRefresher>();
        services.AddSingleton<PullRequestSyncBindingVisitor>();
        services.AddSingleton<PullRequestSyncTickWorkflow>();
        services.AddSingleton<PullRequestAutoBindWorkflow>();
        services.AddSingleton<PullRequestAutoBindOnStopSubscriber>();
        services.AddSingleton<ITerminalHookSubscriber>(sp =>
            sp.GetRequiredService<PullRequestAutoBindOnStopSubscriber>());
        services.AddTerminalServices();
        services.AddSingleton<TerminalHookStatusHandler>();
        services.AddSingleton<ITerminalHookSubscriber>(sp =>
            sp.GetRequiredService<TerminalHookStatusHandler>());
        // ADR-0026 § 8: tmux session is torn down when an intent is closed (`done` or `reject`). The
        // handler takes ITmuxSessionManager via Lazy to break the dispatcher↔handler resolution cycle
        // (TmuxSessionManager → IDomainEventDispatcher → IEnumerable<IDomainEventHandler>).
        services.AddSingleton(sp => new Lazy<ITmuxSessionManager>(sp.GetRequiredService<ITmuxSessionManager>));
        services.AddSingleton<IDomainEventHandler, TerminalKillOnIntentCloseHandler>();
        // Closing an intent also wipes its local state (trust entries + workspace folder),
        // gated by the intent's CleanupLocalStateOnClose flag. Best-effort, sibling to the kill above.
        services.AddSingleton<IDomainEventHandler, IntentLocalStateCleanupOnCloseHandler>();
        services.AddSingleton<SetTagDefaultRepositoriesHandler>();
        services.AddSingleton<GetTagHandler>();
        // Open-in-IDE (ADR-0026 § 7 + carrier-capability ADR): a single carrier
        // capability `open_in_ide` with multiple interchangeable IDE openers
        // (VS Code, Cursor). The opener registry is per-process; concrete
        // IIdeOpener implementations are registered by Infrastructure.
        services.AddSingleton<IIdeOpenerRegistry, IdeOpenerRegistry>();
        services.AddSingleton<OpenInIdeService>();
        services.AddSingleton<ITerminalOpenerRegistry, TerminalOpenerRegistry>();
        services.AddSingleton<OpenInTerminalService>();
        services.AddTaskTrackerAxis();
        services.AddTaskTrackerSync();
        return services;
    }

    private static IServiceCollection AddTerminalServices(this IServiceCollection services)
    {
        // RunPreflightOrchestrator pulls in ITmuxSessionManager from Infrastructure registration.
        services.AddSingleton<CapabilitiesPersistence>();
        services.AddSingleton<CapabilitiesService>();
        // Agent-vendor axis (ADR-0045): each descriptor is registered individually and resolved
        // through the DI-backed catalog — a new vendor is one descriptor + one line here, no
        // central list or switch. Registration order is the launch-surface display order.
        services.AddSingleton(TerminalVendorDescriptors.Claude);
        services.AddSingleton(TerminalVendorDescriptors.Codex);
        services.AddSingleton(TerminalVendorDescriptors.Opencode);
        services.AddSingleton<ITerminalVendorCatalog, TerminalVendorCatalog>();
        // Operational skills (ADR-0045): one descriptor per skill, resolved through the
        // DI-backed catalog — add a skill = new descriptor + folder + one line below.
        services.AddSingleton(SessionSkillDescriptors.Intent);
        services.AddSingleton(SessionSkillDescriptors.Review);
        services.AddSingleton(SessionSkillDescriptors.Dream);
        services.AddSingleton<ISessionSkillCatalog, SessionSkillCatalog>();
        services.AddSingleton<SessionSkillPackageRegistry>();
        services.AddSingleton<SessionSkillSelectionService>();
        services.AddSingleton<RunPreflightSkillPlanner>();
        services.AddSingleton<RunPreflightTagNames>();
        services.AddSingleton<IntentLinkPromptContextReader>();
        services.AddSingleton<IntentWorkspaceMapComposer>();
        services.AddSingleton<RunPreflightLaunchPlanner>();
        services.AddSingleton<SkillModeDefaultsService>();
        services.AddSingleton<TagDefaultsUnion>();
        services.AddSingleton<RunPreflightAutoBind>();
        services.AddSingleton<RunPreflightCloneScheduler>();
        services.AddSingleton<RunPreflightCloneWait>();
        services.AddSingleton<TerminalReadinessSignals>();
        services.AddSingleton<TerminalPromptSubmitSignals>();
        services.AddSingleton<TerminalReadinessSignalSubscriber>();
        services.AddSingleton<ITerminalHookSubscriber>(sp =>
            sp.GetRequiredService<TerminalReadinessSignalSubscriber>());
        services.AddSingleton<TerminalPromptSubmitSignalSubscriber>();
        services.AddSingleton<ITerminalHookSubscriber>(sp =>
            sp.GetRequiredService<TerminalPromptSubmitSignalSubscriber>());
        services.AddSingleton<TmuxTuiReadinessWaiter>();
        services.AddSingleton<TmuxPromptSubmitConfirmer>();
        services.AddSingleton<IRunPreflightPromptDelivery, RunPreflightPromptDelivery>();
        services.AddSingleton<WorkspaceAttachmentDumper>();
        services.AddSingleton<RunPreflightWorkspacePreparer>();
        services.AddSingleton<RunPreflightSpawn>();
        services.AddSingleton<RunPreflightPromptGate>();
        services.AddSingleton<RunPreflightGuards>();
        services.AddSingleton<RunPreflightOrchestrator>();
        services.AddSingleton<AttachIntentTerminalSkillsHandler>();
        services.AddSingleton<TerminalLaunchResolver>();
        services.AddSingleton<TerminalSettingsService>();
        services.AddSingleton<LocalModelDiscoveryService>();
        services.AddSingleton<IVendorModelCatalog, OpencodeLocalModelCatalog>();
        services.AddSingleton<TerminalSessionStatusService>();
        services.AddSingleton<TerminalSessionKillService>();
        return services;
    }

    private static IServiceCollection AddIntentHandlers(this IServiceCollection services)
    {
        services.AddSingleton<CreateIntentHandler>();
        services.AddSingleton<GetIntentHandler>();
        services.AddSingleton<ReadIntentTextHandler>();
        services.AddSingleton<ReplaceIntentTextHandler>();
        services.AddSingleton<DeleteIntentHandler>();
        services.AddSingleton<UploadIntentAttachmentHandler>();
        services.AddSingleton<ListIntentAttachmentsHandler>();
        services.AddSingleton<DownloadIntentAttachmentHandler>();
        services.AddSingleton<DeleteIntentAttachmentHandler>();
        services.AddSingleton<SearchIntentTextHandler>();
        services.AddSingleton<InsertIntentTextAfterLineHandler>();
        services.AddSingleton<ListIntentsHandler>();
        services.AddSingleton<GetIntentContextsHandler>();
        services.AddSingleton<SetIntentStatusHandler>();
        services.AddSingleton<SetIntentCleanupOnDoneHandler>();
        services.AddSingleton<SetIntentTagsHandler>();
        services.AddSingleton<SetIntentTitleHandler>();
        services.AddSingleton<MoveIntentHandler>();
        services.AddSingleton<PinIntentHandler>();
        services.AddSingleton<UnpinIntentHandler>();
        services.AddSingleton<MovePinHandler>();
        services.AddSingleton<LinkIntentHandler>();
        services.AddSingleton<UnlinkIntentHandler>();
        services.AddSingleton<ListIntentLinksHandler>();
        services.AddSingleton<GetIntentLinksSummaryHandler>();
        services.AddSingleton<ListIntentEventsHandler>();
        services.AddSingleton<ListIntentVersionsHandler>();
        return services;
    }
}
