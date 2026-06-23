using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Repositories.Endpoints;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// HTTP surface over the <c>Repository</c> registry and <c>PullRequestArtifact</c> verification
/// artifacts (ADR-0031). This controller maps requests onto the existing application ports.
/// </summary>
public sealed class RepositoriesController(
    ListPullRequestArtifactsEndpoint listPrArtifactsEndpoint,
    GetPullRequestArtifactEndpoint getPrArtifactEndpoint,
    PutPullRequestArtifactEndpoint putPrArtifactEndpoint) : RepositoriesControllerBase
{
    public override Task<ActionResult<ICollection<PullRequestArtifactDto>>> ListPullRequestArtifacts(string binding_id) =>
        listPrArtifactsEndpoint.RunAsync(binding_id, HttpContext.RequestAborted);

    public override Task<ActionResult<PullRequestArtifactDto>> GetPullRequestArtifact(
        string binding_id,
        string type) =>
        getPrArtifactEndpoint.RunAsync(binding_id, type, HttpContext.RequestAborted);

    public override Task<ActionResult<PullRequestArtifactDto>> PutPullRequestArtifact(
        string binding_id,
        string type,
        PutPullRequestArtifactRequest body) =>
        putPrArtifactEndpoint.RunAsync(binding_id, type, body, HttpContext.RequestAborted);
}
