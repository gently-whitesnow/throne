namespace Throne.Application.Git;

/// <summary>
/// Port for a git hosting provider (GitHub / GitLab / …) as defined in ADR-0024 § 3.
/// Implementations shell out to vendor CLI tools (<c>gh</c>, <c>glab</c>) via the
/// <see cref="Ports.IProcessLauncher"/> seam — Throne intentionally does not maintain
/// its own OAuth client. All methods return typed DTOs, never raw CLI output, so
/// consumers can be unit-tested without spawning processes.
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
    /// After the clone the working tree is moved onto <paramref name="checkout"/>:
    /// a PR is switched through the provider CLI (handles fork-PRs), otherwise the
    /// branch is switched through plain git — see <see cref="CloneCheckout"/> for the
    /// placeholder-vs-real-ref semantics.
    /// </summary>
    Task CloneRepositoryAsync(
        string owner,
        string repo,
        string targetPath,
        CloneCheckout checkout,
        CancellationToken ct);

    /// <summary>
    /// Применить <paramref name="checkout"/> к уже склонированному <paramref name="workspacePath"/>
    /// (оператор привязал PR к готовому биндингу). Семантика — как у post-clone шага CloneRepositoryAsync.
    /// </summary>
    Task CheckoutAsync(string owner, string repo, string workspacePath, CloneCheckout checkout, CancellationToken ct);

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
    /// Used by the settings page and the bind-modal precondition check.
    /// </summary>
    Task<ProviderAuthStatus> GetAuthStatusAsync(CancellationToken ct);

    /// <summary>
    /// Fetch the provider-derived diff of an entire pull/merge request as
    /// per-file unified patches plus the SHAs needed to round-trip review comments.
    /// Returns <see langword="null"/> when upstream returns 404.
    /// Backs <c>scope=request</c> of the review-workspace diff endpoint (Slice 4A).
    /// </summary>
    Task<PullRequestDiff?> GetPullRequestDiffAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct);

    /// <summary>
    /// Fetch the diff of a single commit by SHA in the same shape as
    /// <see cref="GetPullRequestDiffAsync"/>. Backs <c>scope=commit</c>.
    /// Returns <see langword="null"/> when upstream returns 404 for the commit.
    /// </summary>
    Task<PullRequestDiff?> GetCommitDiffAsync(
        string owner,
        string repo,
        string commitSha,
        CancellationToken ct);

    /// <summary>
    /// List the commits inside a pull/merge request — backs the per-commit
    /// selector in the review workspace. Returns <see langword="null"/> when
    /// upstream returns 404.
    /// </summary>
    Task<IReadOnlyList<PullRequestCommitRef>?> ListPullRequestCommitsAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct);

    /// <summary>
    /// Submit an inline review comment straight to the provider (no durable storage
    /// on Throne's side). The anchor is provider-neutral and was produced from a
    /// <see cref="PullRequestDiff"/>; provider implementations map it into the
    /// upstream wire format. Returns the echoed comment so the UI can render it
    /// before the next manual refresh of the comments feed.
    /// </summary>
    Task<SubmittedReviewComment> SubmitReviewCommentAsync(
        string owner,
        string repo,
        int number,
        SubmitReviewCommentRequest request,
        CancellationToken ct);

    /// <summary>
    /// Resolve or reopen a review thread at the provider and return the resolution
    /// state read back from its response (no durable storage on Throne's side).
    /// GitHub maps <paramref name="threadId"/> to a review-thread node id and runs
    /// the <c>resolveReviewThread</c>/<c>unresolveReviewThread</c> graphql mutation;
    /// GitLab toggles the discussion's <c>resolved</c> flag. A 404 propagates as a
    /// <see cref="GitProviderException"/> with <see cref="GitProviderErrorKind.NotFound"/>.
    /// </summary>
    Task<ReviewThreadState> ResolveReviewThreadAsync(
        string owner,
        string repo,
        int number,
        string threadId,
        bool resolved,
        CancellationToken ct);

    /// <summary>
    /// Delete a single inline review comment at the provider. GitHub deletes by
    /// <paramref name="commentId"/> alone; GitLab needs <paramref name="threadId"/>
    /// (the discussion the note belongs to). A 404 propagates as a
    /// <see cref="GitProviderException"/> with <see cref="GitProviderErrorKind.NotFound"/>.
    /// </summary>
    Task DeleteReviewCommentAsync(
        string owner,
        string repo,
        int number,
        string commentId,
        string? threadId,
        CancellationToken ct);

    /// <summary>
    /// Read the provider-neutral mergeability and CI-checks state of a pull/merge
    /// request — backs the merge control in the review workspace (Slice C). Returns
    /// <see langword="null"/> when upstream returns 404.
    /// </summary>
    Task<PullRequestMergeStatus?> GetPullRequestMergeStatusAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct);

    /// <summary>
    /// Merge a pull/merge request at the provider with the requested strategy and
    /// optional source-branch deletion (<c>gh pr merge</c> / <c>glab mr merge</c>).
    /// A 404 propagates as <see cref="GitProviderErrorKind.NotFound"/>; a provider
    /// refusal (conflicts, failing checks, branch protection, disallowed strategy)
    /// propagates as <see cref="GitProviderErrorKind.MergeNotAllowed"/> with the
    /// reason in <see cref="GitProviderException.Detail"/>.
    /// </summary>
    Task<PullRequestMergeResult> MergePullRequestAsync(
        string owner,
        string repo,
        int number,
        MergePullRequestRequest request,
        CancellationToken ct);
}
