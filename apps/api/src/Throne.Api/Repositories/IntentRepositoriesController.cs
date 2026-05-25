using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Repositories.Endpoints;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

public sealed class IntentRepositoriesController(
    ListIntentRepositoriesEndpoint listEndpoint,
    BindIntentRepositoryEndpoint bindEndpoint,
    UnbindIntentRepositoryEndpoint unbindEndpoint,
    SyncIntentRepositoryPullRequestEndpoint syncEndpoint,
    ListIntentRepositoryPullRequestCommentsEndpoint listCommentsEndpoint) : IntentRepositoriesControllerBase
{
    public override Task<ActionResult<ICollection<RepositoryBindingDto>>> ListIntentRepositories(string intent_id) =>
        listEndpoint.RunAsync(intent_id, HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryBindingDto>> BindIntentRepository(
        string intent_id,
        BindIntentRepositoryRequest body) =>
        bindEndpoint.RunAsync(intent_id, body, Url, HttpContext.RequestAborted);

    public override Task<IActionResult> UnbindIntentRepository(string intent_id, string binding_id) =>
        unbindEndpoint.RunAsync(intent_id, binding_id, HttpContext.RequestAborted);

    public override Task<ActionResult<PullRequestSyncResultDto>> SyncIntentRepositoryPullRequest(
        string intent_id,
        string binding_id) =>
        syncEndpoint.RunAsync(intent_id, binding_id, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<PullRequestCommentDto>>> ListIntentRepositoryPullRequestComments(
        string intent_id,
        string binding_id,
        DateTimeOffset? since = null) =>
        listCommentsEndpoint.RunAsync(intent_id, binding_id, since, HttpContext.RequestAborted);
}
