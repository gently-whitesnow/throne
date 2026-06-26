using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Infrastructure.Git.GitHubCli;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Git;

internal static class GitInfrastructureModule
{
    public static void AddThroneGitInfrastructure(
        IServiceCollection services,
        IConfiguration? configuration)
    {
        var workspaceBuilder = services.AddOptions<WorkspaceOptions>();
        var githubBuilder = services.AddOptions<GitHubCliOptions>();
        var gitlabCliBuilder = services.AddOptions<GitLabCliOptions>();
        var prSyncBuilder = services.AddOptions<PullRequestSyncOptions>();
        var cloneRunnerBuilder = services.AddOptions<CloneRunnerOptions>();
        if (configuration is not null)
        {
            workspaceBuilder.Bind(configuration.GetSection(WorkspaceOptions.SectionName));
            githubBuilder.Bind(configuration.GetSection(GitHubCliOptions.SectionName));
            gitlabCliBuilder.Bind(configuration.GetSection(GitLabCliOptions.SectionName));
            prSyncBuilder.Bind(configuration.GetSection(PullRequestSyncOptions.SectionName));
            cloneRunnerBuilder.Bind(configuration.GetSection(CloneRunnerOptions.SectionName));
        }

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PullRequestSyncOptions>>().Value);

        services.AddSingleton<WorkspaceRootInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRootInitializer>());
        services.AddSingleton<IWorkspaceRootProvider>(sp => sp.GetRequiredService<WorkspaceRootInitializer>());
        services.AddSingleton<IWorkspaceDirectoryRemover, WorkspaceDirectoryRemover>();
        services.AddSingleton<IWorkspaceDirectorySizer, WorkspaceDirectorySizer>();
        services.AddSingleton<IWorkspaceDirectoryProbe, WorkspaceDirectoryProbe>();
        services.AddSingleton<IProcessLauncher, ProcessRunner>();
        services.AddSingleton<ILocalGitBranchReader, LocalGitBranchReader>();
        services.AddSingleton<ILocalGitWorkspaceSync, LocalGitWorkspaceSync>();
        services.AddSingleton<GitCheckoutRunner>();
        services.AddSingleton<IRepositoryBlobReader, LocalGitBlobReader>();
        services.AddSingleton<GhCliInvoker>();
        services.AddSingleton<GhRepoListExecutor>();
        services.AddSingleton<GhRepoSearcher>();
        services.AddSingleton<GhRepoActions>();
        services.AddSingleton<GhAuthProbe>();
        services.AddSingleton<GhReviewThreadsReader>();
        services.AddSingleton<GhPullRequestActions>();
        services.AddSingleton<GhReviewWorkspaceActions>();
        services.AddSingleton<GhMergeActions>();
        services.AddSingleton<GhBranchLister>();
        services.AddSingleton<GhPullRequestLister>();
        services.AddSingleton<GhRefListers>();
        services.AddSingleton<IGitProvider, GitHubCliProvider>();
        // IGitLabHostProvider is wired by the persistence backend.
        // because it owns the settings storage. Git infrastructure stays storage-agnostic.
        services.AddSingleton<GlabCliInvoker>();
        services.AddSingleton<GlabRepoSearcher>();
        services.AddSingleton<GlabRepoActions>();
        services.AddSingleton<GlabAuthProbe>();
        services.AddSingleton<GlabPullRequestActions>();
        services.AddSingleton<GlabReviewWorkspaceActions>();
        services.AddSingleton<GlabMergeActions>();
        services.AddSingleton<GlabBranchLister>();
        services.AddSingleton<GlabPullRequestLister>();
        services.AddSingleton<GlabRefListers>();
        services.AddSingleton<IGitProvider, GitLabCliProvider>();
        services.AddSingleton<IGitProviderRegistry, GitProviderRegistry>();
        services.AddHostedService<RepositoryCloneService>();
        services.AddHostedService<PullRequestSyncService>();
    }
}
