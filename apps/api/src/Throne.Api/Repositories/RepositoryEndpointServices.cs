using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Repositories.Endpoints;
using Throne.Api.Settings;
using Throne.Api.Settings.Endpoints;
using Throne.Application.Repositories;

namespace Throne.Api.Repositories;

internal static class RepositoryEndpointServices
{
    public static IServiceCollection AddThroneRepositoryEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SearchGitProviderRepositoriesEndpoint>();
        services.AddSingleton<ListGitProviderRepositoriesEndpoint>();
        services.AddSingleton<ListGitProviderRepositoryBranchesEndpoint>();
        services.AddSingleton<ListGitProviderRepositoryPullRequestsEndpoint>();
        services.AddSingleton<ListIntentRepositoriesEndpoint>();
        services.AddSingleton<BindIntentRepositoryEndpoint>();
        services.AddSingleton<UnbindIntentRepositoryEndpoint>();
        services.AddSingleton<SyncIntentRepositoryPullRequestEndpoint>();
        services.AddSingleton<AttachIntentRepositoryPullRequestEndpoint>();
        services.AddSingleton<ListIntentRepositoryPullRequestCommentsEndpoint>();
        services.AddSingleton<ListPullRequestCommentsUseCase>();
        services.AddSingleton<GetReviewWorkspaceDiffUseCase>();
        services.AddSingleton<ListReviewWorkspaceCommitsUseCase>();
        services.AddSingleton<SubmitReviewWorkspaceCommentUseCase>();
        services.AddSingleton<GetIntentRepositoryReviewDiffEndpoint>();
        services.AddSingleton<ListIntentRepositoryReviewCommitsEndpoint>();
        services.AddSingleton<SubmitIntentRepositoryReviewCommentEndpoint>();

        services.AddSingleton<ListRepositoriesEndpoint>();
        services.AddSingleton<CreateRepositoryEndpoint>();
        services.AddSingleton<GetRepositoryEndpoint>();
        services.AddSingleton<ListRepositoryDocumentsEndpoint>();
        services.AddSingleton<GetRepositoryDocumentEndpoint>();
        services.AddSingleton<PutRepositoryDocumentEndpoint>();
        services.AddSingleton<ListRepositoryDocumentVersionsEndpoint>();

        services.AddSingleton<WorkspaceSizeProbe>();
        services.AddSingleton<GetWorkspaceSettingsEndpoint>();
        services.AddSingleton<CleanWorkspaceEndpoint>();
        services.AddSingleton<GetGitProvidersStatusEndpoint>();

        return services;
    }
}
