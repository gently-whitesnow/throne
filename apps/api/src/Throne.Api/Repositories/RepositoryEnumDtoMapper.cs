using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Wire-format ↔ Application enum translation for the repositories module.
/// </summary>
internal static class RepositoryEnumDtoMapper
{
    public static GitProvider ToWireProvider(string value) => value switch
    {
        GitProviderNames.GitHub => GitProvider.Github,
        GitProviderNames.GitLab => GitProvider.Gitlab,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown git provider."),
    };

    public static string ToProviderName(GitProvider provider) => provider switch
    {
        GitProvider.Github => GitProviderNames.GitHub,
        GitProvider.Gitlab => GitProviderNames.GitLab,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown git provider."),
    };

    public static CloneStatus ToWireCloneStatus(string value) => value switch
    {
        CloneStatusNames.Pending => CloneStatus.Pending,
        CloneStatusNames.Cloning => CloneStatus.Cloning,
        CloneStatusNames.Ready => CloneStatus.Ready,
        CloneStatusNames.Failed => CloneStatus.Failed,
        CloneStatusNames.Broken => CloneStatus.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown clone status."),
    };

    public static PullRequestState ToWirePullRequestState(string value) => value switch
    {
        PullRequestStateNames.Open => PullRequestState.Open,
        PullRequestStateNames.Closed => PullRequestState.Closed,
        PullRequestStateNames.Merged => PullRequestState.Merged,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown PR state."),
    };

    public static PullRequestMergeability ToWireMergeability(Throne.Application.Git.PullRequestMergeability value) => value switch
    {
        Throne.Application.Git.PullRequestMergeability.Mergeable => PullRequestMergeability.Mergeable,
        Throne.Application.Git.PullRequestMergeability.Conflicting => PullRequestMergeability.Conflicting,
        Throne.Application.Git.PullRequestMergeability.Blocked => PullRequestMergeability.Blocked,
        Throne.Application.Git.PullRequestMergeability.Behind => PullRequestMergeability.Behind,
        Throne.Application.Git.PullRequestMergeability.Checking => PullRequestMergeability.Checking,
        _ => PullRequestMergeability.Unknown,
    };

    public static PullRequestChecksState ToWireChecksState(Throne.Application.Git.PullRequestChecksState value) => value switch
    {
        Throne.Application.Git.PullRequestChecksState.None => PullRequestChecksState.None,
        Throne.Application.Git.PullRequestChecksState.Pending => PullRequestChecksState.Pending,
        Throne.Application.Git.PullRequestChecksState.Passing => PullRequestChecksState.Passing,
        Throne.Application.Git.PullRequestChecksState.Failing => PullRequestChecksState.Failing,
        _ => PullRequestChecksState.Unknown,
    };

    public static Throne.Application.Git.MergeStrategy ToAppMergeStrategy(MergeStrategy value) => value switch
    {
        MergeStrategy.Merge => Throne.Application.Git.MergeStrategy.Merge,
        MergeStrategy.Squash => Throne.Application.Git.MergeStrategy.Squash,
        MergeStrategy.Rebase => Throne.Application.Git.MergeStrategy.Rebase,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown merge strategy."),
    };

    public static RepositoryArtifactRenderHint ToWireRenderHint(string value) => value switch
    {
        RepositoryArtifactRenderHints.Markdown => RepositoryArtifactRenderHint.Markdown,
        RepositoryArtifactRenderHints.SchemaMap => RepositoryArtifactRenderHint.Schema_map,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown render hint."),
    };
}
