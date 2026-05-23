using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Repositories.Endpoints;
using Throne.Api.Settings;
using Throne.Api.Settings.Endpoints;

namespace Throne.Api.Repositories;

/// <summary>
/// DI registration for the per-endpoint classes used by the repositories /
/// settings HTTP modules (T-11). Mirrors <c>IntentEndpointServices</c>: per-endpoint
/// instances are Singletons since they wrap singleton application handlers /
/// services.
/// </summary>
internal static class RepositoryEndpointServices
{
    public static IServiceCollection AddThroneRepositoryEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SearchGithubRepositoriesEndpoint>();
        services.AddSingleton<ListMyGithubRepositoriesEndpoint>();
        services.AddSingleton<ListIntentRepositoriesEndpoint>();
        services.AddSingleton<BindIntentRepositoryEndpoint>();
        services.AddSingleton<UnbindIntentRepositoryEndpoint>();
        services.AddSingleton<SyncIntentRepositoryPullRequestEndpoint>();
        services.AddSingleton<ListIntentRepositoryPullRequestCommentsEndpoint>();

        services.AddSingleton<WorkspaceSizeProbe>();
        services.AddSingleton<GetWorkspaceSettingsEndpoint>();
        services.AddSingleton<GetGitProvidersStatusEndpoint>();

        return services;
    }
}
