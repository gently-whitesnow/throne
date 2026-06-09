using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Repositories.Endpoints;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

public sealed class IntentRepositoriesController(
    ListIntentRepositoriesEndpoint listEndpoint,
    BindIntentRepositoryEndpoint bindEndpoint,
    UnbindIntentRepositoryEndpoint unbindEndpoint,
    RefreshIntentRepositoryEndpoint refreshEndpoint,
    SyncIntentRepositoryPullRequestEndpoint syncEndpoint,
    AttachIntentRepositoryPullRequestEndpoint attachPrEndpoint,
    ListIntentRepositoryPullRequestCommentsEndpoint listCommentsEndpoint,
    GetIntentRepositoryReviewDiffEndpoint reviewDiffEndpoint,
    ListIntentRepositoryReviewCommitsEndpoint reviewCommitsEndpoint,
    SubmitIntentRepositoryReviewCommentEndpoint submitReviewCommentEndpoint,
    DeleteIntentRepositoryPullRequestCommentEndpoint deleteReviewCommentEndpoint,
    UpdateIntentRepositoryReviewThreadEndpoint updateReviewThreadEndpoint) : IntentRepositoriesControllerBase
{
    public override Task<ActionResult<ICollection<RepositoryBindingDto>>> ListIntentRepositories(string intent_id) =>
        listEndpoint.RunAsync(intent_id, HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryBindingDto>> BindIntentRepository(
        string intent_id,
        BindIntentRepositoryRequest body) =>
        bindEndpoint.RunAsync(intent_id, body, Url, HttpContext.RequestAborted);

    public override Task<IActionResult> UnbindIntentRepository(string intent_id, string binding_id) =>
        unbindEndpoint.RunAsync(intent_id, binding_id, HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryBindingDto>> RefreshIntentRepository(
        string intent_id,
        string binding_id) =>
        refreshEndpoint.RunAsync(intent_id, binding_id, HttpContext.RequestAborted);

    public override Task<ActionResult<PullRequestSyncResultDto>> SyncIntentRepositoryPullRequest(
        string intent_id,
        string binding_id) =>
        syncEndpoint.RunAsync(intent_id, binding_id, HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryBindingDto>> AttachIntentRepositoryPullRequest(
        string intent_id,
        string binding_id,
        AttachIntentRepositoryPullRequestRequest body) =>
        attachPrEndpoint.RunAsync(intent_id, binding_id, body, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<PullRequestCommentDto>>> ListIntentRepositoryPullRequestComments(
        string intent_id,
        string binding_id,
        DateTimeOffset? since = null) =>
        listCommentsEndpoint.RunAsync(intent_id, binding_id, since, HttpContext.RequestAborted);

    public override Task<ActionResult<PullRequestDiffDto>> GetIntentRepositoryReviewDiff(
        string intent_id,
        string binding_id,
        ReviewDiffScope? scope = null,
        string? commit_sha = null) =>
        reviewDiffEndpoint.RunAsync(
            intent_id,
            binding_id,
            scope is null ? null : ToAppScope(scope.Value),
            commit_sha,
            HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<PullRequestCommitDto>>> ListIntentRepositoryReviewCommits(
        string intent_id,
        string binding_id) =>
        reviewCommitsEndpoint.RunAsync(intent_id, binding_id, HttpContext.RequestAborted);

    public override Task<ActionResult<SubmittedReviewCommentDto>> SubmitIntentRepositoryReviewComment(
        string intent_id,
        string binding_id,
        SubmitReviewCommentRequest body) =>
        submitReviewCommentEndpoint.RunAsync(intent_id, binding_id, body, HttpContext.RequestAborted);

    public override Task<IActionResult> DeleteIntentRepositoryPullRequestComment(
        string intent_id,
        string binding_id,
        string comment_id,
        string thread_id = null!) =>
        deleteReviewCommentEndpoint.RunAsync(intent_id, binding_id, comment_id, thread_id, HttpContext.RequestAborted);

    public override Task<ActionResult<ReviewThreadDto>> UpdateIntentRepositoryReviewThread(
        string intent_id,
        string binding_id,
        string thread_id,
        UpdateReviewThreadRequest body) =>
        updateReviewThreadEndpoint.RunAsync(intent_id, binding_id, thread_id, body, HttpContext.RequestAborted);

    private static Throne.Application.Repositories.ReviewDiffScope ToAppScope(ReviewDiffScope wire) => wire switch
    {
        ReviewDiffScope.Commit => Throne.Application.Repositories.ReviewDiffScope.Commit,
        _ => Throne.Application.Repositories.ReviewDiffScope.Request,
    };
}
