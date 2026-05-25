namespace Throne.Application.Git;

/// <summary>
/// Port for a git hosting provider (GitHub / GitLab / …) as defined in ADR-0024 § 3.
/// Implementations shell out to vendor CLI tools (<c>gh</c>, <c>glab</c>) via the
/// <see cref="Ports.IProcessLauncher"/> seam — Throne intentionally does not maintain
/// its own OAuth client.
///
/// All methods return typed DTOs, never raw CLI output, so consumers (clone /
/// PR-sync background services in slice 1) can be unit-tested without spawning
/// processes. The matching implementations land in T-06 / T-07.
/// </summary>
public interface IGitProvider
{
    /// <summary>
    /// Provider key this implementation handles. Must match one of
    /// <c>Throne.Domain.Repositories.GitProviderNames</c> constants and is the key
    /// used by <see cref="IGitProviderRegistry"/>.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Search repositories the authenticated user can see. <paramref name="query"/>
    /// is an optional case-insensitive substring filter; <paramref name="limit"/>
    /// caps the page size (provider may return fewer).
    /// </summary>
    Task<IReadOnlyList<GitRepositoryRef>> SearchRepositoriesAsync(
        RepositorySearchScope scope,
        string? query,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Shortcut for <c>scope=Mine</c> with no query — backs the default state of
    /// the bind-repository modal (operationId <c>listMyGithubRepositories</c>).
    /// </summary>
    Task<IReadOnlyList<GitRepositoryRef>> ListUserRepositoriesAsync(
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Clone the repository into <paramref name="targetPath"/>. Caller owns
    /// directory placement (see <c>WorkspaceRootInitializer</c> and the
    /// <c>{root}/intents/{intent_id}/{owner}__{repo}/</c> layout from ADR-0024).
    /// </summary>
    Task CloneRepositoryAsync(
        string owner,
        string repo,
        string targetPath,
        CancellationToken ct);

    /// <summary>
    /// Synchronize an already-cloned <paramref name="workspacePath"/> using the provider CLI.
    /// </summary>
    Task SyncRepositoryAsync(string workspacePath, CancellationToken ct);

    /// <summary>
    /// List branches of <paramref name="owner"/>/<paramref name="repo"/> for the
    /// typeahead in the bind-repository modal. <paramref name="query"/> is an
    /// optional case-insensitive substring filter applied to branch name;
    /// <paramref name="limit"/> caps the page size. The repository's default
    /// branch is marked on the matching entry (<see cref="GitBranchRef.IsDefault"/>).
    /// </summary>
    Task<IReadOnlyList<GitBranchRef>> ListBranchesAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// List open pull requests of <paramref name="owner"/>/<paramref name="repo"/>
    /// for the typeahead in the bind-repository modal. Only <c>state=open</c>
    /// is returned; <paramref name="query"/> is an optional case-insensitive
    /// substring filter over <c>#{number}</c> / title / head ref.
    /// </summary>
    Task<IReadOnlyList<GitPullRequestRef>> ListPullRequestsAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Read a single pull request snapshot. Returns <see langword="null"/> when
    /// upstream returns 404 — callers (sync service) translate that into
    /// <c>clone_status=broken</c> per ADR-0024 § 7.
    /// </summary>
    Task<PullRequestSnapshot?> GetPullRequestAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct);

    /// <summary>
    /// Fetch PR review comments with optional <paramref name="etag"/> for
    /// conditional GET. A 304 response is encoded as
    /// <see cref="PullRequestCommentsPage.NotModified"/> so callers can skip fanout
    /// without comparing payloads. A 404 propagates as <see langword="null"/>.
    /// </summary>
    Task<PullRequestCommentsPage?> ListPullRequestCommentsAsync(
        string owner,
        string repo,
        int number,
        string? etag,
        CancellationToken ct);

    /// <summary>
    /// Probe the vendor CLI's auth state without performing any side effect.
    /// Used by the settings page (T-16) and the bind-modal precondition check.
    /// </summary>
    Task<ProviderAuthStatus> GetAuthStatusAsync(CancellationToken ct);
}
