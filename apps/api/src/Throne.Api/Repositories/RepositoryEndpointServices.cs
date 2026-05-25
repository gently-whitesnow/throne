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

        services.AddSingleton<SearchGithubRepositoriesEndpoint>();
        services.AddSingleton<ListMyGithubRepositoriesEndpoint>();
        services.AddSingleton<ListGithubRepositoryBranchesEndpoint>();
        services.AddSingleton<ListGithubRepositoryPullRequestsEndpoint>();
        services.AddSingleton<ListIntentRepositoriesEndpoint>();
        services.AddSingleton<BindIntentRepositoryEndpoint>();
        services.AddSingleton<UnbindIntentRepositoryEndpoint>();
        services.AddSingleton<SyncIntentRepositoryPullRequestEndpoint>();
        services.AddSingleton<ListIntentRepositoryPullRequestCommentsEndpoint>();
        services.AddSingleton<ListPullRequestCommentsUseCase>();

        services.AddSingleton<WorkspaceSizeProbe>();
        services.AddSingleton<GetWorkspaceSettingsEndpoint>();
        services.AddSingleton<GetGitProvidersStatusEndpoint>();

        return services;
    }
}
