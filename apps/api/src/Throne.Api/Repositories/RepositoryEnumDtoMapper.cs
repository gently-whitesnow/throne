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
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown git provider."),
    };

    public static string ToProviderName(GitProvider provider) => provider switch
    {
        GitProvider.Github => GitProviderNames.GitHub,
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

    public static RepositoryArtifactRenderHint ToWireRenderHint(string value) => value switch
    {
        RepositoryArtifactRenderHints.Markdown => RepositoryArtifactRenderHint.Markdown,
        RepositoryArtifactRenderHints.SchemaMap => RepositoryArtifactRenderHint.Schema_map,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown render hint."),
    };
}
