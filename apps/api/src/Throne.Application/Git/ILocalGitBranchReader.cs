namespace Throne.Application.Git;

/// <summary>
/// Reads the currently checked-out branch of a local clone
/// (<c>git -C {workspacePath} rev-parse --abbrev-ref HEAD</c>). Provider-agnostic plain
/// git, kept out of <see cref="IGitProvider"/> (which is the vendor-API surface). Backs the
/// PR auto-bind pass: the agent's pushed branch is matched against open PRs on the remote.
/// </summary>
public interface ILocalGitBranchReader
{
    /// <summary>
    /// Current branch name, or <see langword="null"/> when it cannot be determined (detached
    /// HEAD reports <c>HEAD</c> and is treated as null, missing repo, or git failure). Callers
    /// skip auto-bind for a null result.
    /// </summary>
    Task<string?> ReadCurrentBranchAsync(string workspacePath, CancellationToken ct);
}
