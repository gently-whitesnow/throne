using Throne.Application.Git;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Computes the absolute on-disk workspace path for an
/// <see cref="IntentRepositoryBinding"/> using the flat-namespace layout fixed by
/// ADR-0024 § 1:
///
/// <code>{Throne:Workspace:Root}/intents/{intent_id}/{owner}__{repo}/</code>
///
/// The double-underscore segment between <c>owner</c> and <c>repo</c> is the
/// collision-proof anchor described in the ADR for GitHub. GitLab namespaces may
/// contain <c>/</c>; those slashes are flattened to <c>-</c> because the directory
/// name is never parsed back into a coordinate.
/// </summary>
internal static class WorkspacePathLayout
{
    public const string IntentsSegment = "intents";
    public const string OwnerRepoSeparator = "__";

    public static string Compute(string workspaceRoot, IntentId intentId, RepoCoordinate coordinate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(coordinate);

        return Path.Combine(
            workspaceRoot,
            IntentsSegment,
            intentId.Value,
            $"{WorkspaceSafeOwner(coordinate)}{OwnerRepoSeparator}{coordinate.Repo}");
    }

    private static string WorkspaceSafeOwner(RepoCoordinate coordinate) =>
        coordinate.Provider == GitProviderNames.GitLab
            ? coordinate.Owner.Replace('/', '-')
            : coordinate.Owner;
}
