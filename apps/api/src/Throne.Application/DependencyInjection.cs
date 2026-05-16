using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Auth;
using Throne.Application.Dreams;
using Throne.Application.Events;
using Throne.Application.InstructionPatches;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Intents.Events;
using Throne.Application.Intents.Linking;
using Throne.Application.Ports;
using Throne.Application.Tags;
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
        services.AddSingleton<ListTagsHandler>();
        services.AddSingleton<CreateTagHandler>();
        services.AddSingleton<RenameTagHandler>();
        services.AddSingleton<DeleteTagHandler>();
        services.AddSingleton<GetTagUsageHandler>();
        services.AddSingleton<ListIntentVersionsHandler>();
        services.AddSingleton<IntentStatusAutoTransition>();
        services.AddSingleton<UserBundleEntries>();
        services.AddSingleton<GetInstructionBundleHandler>();
        services.AddSingleton<GetBundlesTreeHandler>();
        services.AddSingleton<ListInstructionsHandler>();
        services.AddSingleton<GetInstructionHandler>();
        services.AddSingleton<ReplaceInstructionTextHandler>();
        services.AddSingleton<CreateInstructionHandler>();
        services.AddSingleton<ListInstructionVersionsHandler>();
        services.AddSingleton<PersonalAccessTokenSecretFactory>();
        services.AddSingleton<IPersonalAccessTokenResolver, PersonalAccessTokenResolver>();
        services.AddSingleton<GenerateMcpTokenHandler>();
        services.AddSingleton<GetMcpTokenMetaHandler>();
        // Lazy<IUnitOfWork> breaks the singleton-resolution cycle: the
        // IUnitOfWork factory pulls in IDomainEventDispatcher, which pulls in
        // every IDomainEventHandler — and these two handlers themselves need
        // an IUnitOfWork to commit their cascade. Without Lazy, MS DI would
        // recurse through its own resolution lock and deadlock under load.
        services.AddSingleton(sp => new Lazy<IUnitOfWork>(sp.GetRequiredService<IUnitOfWork>));
        // InstructionPatch handlers (ADR-0021 supersedes ADR-0011): supersede the
        // DreamRun + DreamProposal pair with a flat first-class entity.
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
        return services;
    }
}
