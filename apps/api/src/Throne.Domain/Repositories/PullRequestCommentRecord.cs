using Throne.Domain.Intents;

namespace Throne.Domain.Repositories;

/// <summary>
/// Persisted review comment for the pull request attached to an
/// <see cref="IntentRepositoryBinding"/>. Lives in the
/// <c>pull_request_comments</c> collection owned by the background
/// <c>PullRequestSyncService</c> (T-10, ADR-0024 § 6). Wire shape mirrors the
/// <c>PullRequestCommentDto</c> from
/// <c>specs/contracts/repositories/openapi.yaml</c> so the MCP / HTTP read paths
/// (T-11 / T-13) can project to the contract DTO 1:1.
/// </summary>
/// <param name="BindingId">Identifier of the owning binding.</param>
/// <param name="IntentId">Identifier of the owning intent — denormalised so per-intent
/// listings (MCP <c>list_intent_pr_comments</c>, T-13) stay a single Mongo lookup.</param>
/// <param name="UpstreamId">Upstream review-comment id (stringified for wire stability).
/// Composite uniqueness key together with <see cref="BindingId"/>.</param>
/// <param name="AuthorLogin">Provider login of the comment author.</param>
/// <param name="Body">Comment body (Markdown).</param>
/// <param name="CreatedAt">UTC timestamp of comment creation upstream.</param>
/// <param name="ObservedAt">UTC timestamp when Throne first stored the comment locally —
/// drives «new since» ordering for the realtime fanout.</param>
/// <param name="AuthorAvatarUrl">Avatar URL when provider exposes one.</param>
/// <param name="HtmlUrl">Browser-facing URL of the comment.</param>
/// <param name="Path">File path the review comment is anchored to.</param>
/// <param name="UpdatedAt">UTC timestamp of last upstream edit, when available.</param>
public sealed record PullRequestCommentRecord(
    BindingId BindingId,
    IntentId IntentId,
    string UpstreamId,
    string AuthorLogin,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset ObservedAt,
    string? AuthorAvatarUrl = null,
    string? HtmlUrl = null,
    string? Path = null,
    DateTimeOffset? UpdatedAt = null);
