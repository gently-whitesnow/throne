using Microsoft.Extensions.DependencyInjection;
using Throne.Application.DreamRuns;
using Throne.Application.Events;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Tags;
using Throne.Application.TextVersions;

namespace Throne.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddThroneApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
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
        services.AddSingleton<AddIntentQaHandler>();
        services.AddSingleton<AddIntentReviewHandler>();
        services.AddSingleton<ListIntentQaHandler>();
        services.AddSingleton<ListIntentReviewsHandler>();
        services.AddSingleton<ListIntentsHandler>();
        services.AddSingleton<SetIntentStatusHandler>();
        services.AddSingleton<SetIntentTagsHandler>();
        services.AddSingleton<ListTagsHandler>();
        services.AddSingleton<CreateTagHandler>();
        services.AddSingleton<RenameTagHandler>();
        services.AddSingleton<DeleteTagHandler>();
        services.AddSingleton<GetTagUsageHandler>();
        services.AddSingleton<ListIntentVersionsHandler>();
        services.AddSingleton<GetInstructionBundleHandler>();
        services.AddSingleton<GetSkillsTreeHandler>();
        services.AddSingleton<ListInstructionsHandler>();
        services.AddSingleton<GetInstructionHandler>();
        services.AddSingleton<ReplaceInstructionTextHandler>();
        services.AddSingleton<ListInstructionVersionsHandler>();
        services.AddSingleton<ContextTokenCounter>();
        services.AddSingleton<DreamWindowResolver>();
        services.AddSingleton<GetDreamReadinessHandler>();
        services.AddSingleton<ListPendingDreamRunsHandler>();
        services.AddSingleton<GetPendingProposalsCountHandler>();
        services.AddSingleton<GetDreamRunHandler>();
        services.AddSingleton<ApplyDreamProposalHandler>();
        services.AddSingleton<SkipDreamProposalHandler>();
        services.AddSingleton<CloseDreamRunHandler>();
        services.AddSingleton<RunDreamHandler>();
        services.AddSingleton<ProposeDreamRuleHandler>();
        services.AddSingleton<CloseEmptyDreamRunHandler>();
        return services;
    }
}
