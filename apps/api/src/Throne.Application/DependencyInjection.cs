using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Dreams;
using Throne.Application.Events;
using Throne.Application.InstructionPatches;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Intents.Events;
using Throne.Application.Intents.Linking;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Application.Tags;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Application.TextVersions;
using Throne.Application.Vscode;

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
        services.AddSingleton<GetTagUsageHandler>();
        services.AddSingleton<UserBundleEntries>();
        services.AddSingleton<GetInstructionBundleHandler>();
        services.AddSingleton<GetBundlesTreeHandler>();
        services.AddSingleton<ListInstructionsHandler>();
        services.AddSingleton<GetInstructionHandler>();
        services.AddSingleton<ReplaceInstructionTextHandler>();
        services.AddSingleton<CreateInstructionHandler>();
        services.AddSingleton<ListInstructionVersionsHandler>();
        // Lazy<IUnitOfWork> breaks the singleton-resolution cycle: the
        // IUnitOfWork factory pulls in IDomainEventDispatcher, which pulls in
        // every IDomainEventHandler — and these two handlers themselves need
        // an IUnitOfWork to commit their cascade. Without Lazy, MS DI would
        // recurse through its own resolution lock and deadlock under load.
        services.AddSingleton(sp => new Lazy<IUnitOfWork>(sp.GetRequiredService<IUnitOfWork>));
        // InstructionPatch handlers (ADR-0021 supersedes ADR-0011): supersede the
        // DreamRun + DreamProposal pair with a flat first-class entity.
        services.AddSingleton<UserInstructionLookup>();
        services.AddSingleton<ProposeInstructionPatchHandler>();
        services.AddSingleton<ApplyInstructionPatchWorkflow>();
        services.AddSingleton<ApplyInstructionPatchHandler>();
        services.AddSingleton<RejectInstructionPatchHandler>();
        services.AddSingleton<ListInstructionPatchesHandler>();
        services.AddSingleton<GetInstructionPatchHandler>();
        services.AddSingleton<GetCurrentInstructionHandler>();
        // DreamSession handlers (ADR-0022): the frontier agent reads dialogs
        // locally and records its own memory of each /dream pass through MCP.
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
        services.AddSingleton<RepositoryArtifactWriter>();
        services.AddSingleton<GetRepositoryDocumentHandler>();
        services.AddSingleton<ListRepositoriesHandler>();
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
        // Capability orchestrator + Slice 2 Run pre-flight (terminal).
        // RunPreflightOrchestrator pulls in ITmuxSessionManager from
        // Throne.Infrastructure.Terminals — registration there is required.
        services.AddSingleton<CapabilitiesPersistence>();
        services.AddSingleton<CapabilitiesService>();
        services.AddSingleton<TagDefaultsUnion>();
        services.AddSingleton<RunPreflightAutoBind>();
        services.AddSingleton<RunPreflightCloneScheduler>();
        services.AddSingleton<RunPreflightCloneWait>();
        services.AddSingleton<RunPreflightSpawn>();
        services.AddSingleton<RunPreflightGuards>();
        services.AddSingleton<RunPreflightOrchestrator>();
        services.AddSingleton<TerminalSessionStatusService>();
        services.AddSingleton<TerminalSessionKillService>();
        // ADR-0026 § 8: tmux session is torn down when an intent reaches `done`. The handler
        // takes ITmuxSessionManager via Lazy to break the dispatcher↔handler resolution cycle
        // (TmuxSessionManager → IDomainEventDispatcher → IEnumerable<IDomainEventHandler>).
        services.AddSingleton(sp => new Lazy<ITmuxSessionManager>(sp.GetRequiredService<ITmuxSessionManager>));
        services.AddSingleton<IDomainEventHandler, TerminalKillOnIntentDoneHandler>();
        services.AddSingleton<SetTagDefaultRepositoriesHandler>();
        services.AddSingleton<GetTagHandler>();
        // VS Code shell-out (Slice 2 / ADR-0026 § 7). Capability-gated by
        // `capabilities.vscode` (toggle + live `code --version` probe).
        services.AddSingleton<VscodeCapabilityGuard>();
        services.AddSingleton<VscodeSpawner>();
        services.AddSingleton<OpenInVscodeService>();
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
        services.AddSingleton<SetIntentTagsHandler>();
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
        services.AddSingleton<IntentStatusAutoTransition>();
        return services;
    }
}
