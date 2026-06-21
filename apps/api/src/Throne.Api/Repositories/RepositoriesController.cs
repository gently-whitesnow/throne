using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Repositories.Endpoints;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// HTTP surface over the <c>Repository</c> registry and <c>RepositoryArtifact</c> knowledge
/// pages (ADR-0031). This controller maps requests onto the existing application ports.
/// </summary>
public sealed class RepositoriesController(
    ListRepositoriesEndpoint listEndpoint,
    CreateRepositoryEndpoint createEndpoint,
    GetRepositoryEndpoint getEndpoint,
    ListRepositoryDocumentsEndpoint listDocumentsEndpoint,
    GetRepositoryDocumentEndpoint getDocumentEndpoint,
    PutRepositoryDocumentEndpoint putDocumentEndpoint,
    ListRepositoryDocumentVersionsEndpoint listVersionsEndpoint,
    ListPullRequestArtifactsEndpoint listPrArtifactsEndpoint,
    GetPullRequestArtifactEndpoint getPrArtifactEndpoint,
    PutPullRequestArtifactEndpoint putPrArtifactEndpoint) : RepositoriesControllerBase
{
    public override Task<ActionResult<ICollection<RepositoryDto>>> ListRepositories() =>
        listEndpoint.RunAsync(HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryDto>> CreateRepository(CreateRepositoryRequest body) =>
        createEndpoint.RunAsync(body, Url, HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryDto>> GetRepository(GitProvider provider, string owner, string repo) =>
        getEndpoint.RunAsync(provider, owner, repo, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<RepositoryDocumentSummaryDto>>> ListRepositoryDocuments(
        GitProvider provider, string owner, string repo) =>
        listDocumentsEndpoint.RunAsync(provider, owner, repo, HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryDocumentDto>> GetRepositoryDocument(
        GitProvider provider, string owner, string repo, string slug) =>
        getDocumentEndpoint.RunAsync(provider, owner, repo, slug, HttpContext.RequestAborted);

    public override Task<ActionResult<RepositoryDocumentDto>> PutRepositoryDocument(
        GitProvider provider, string owner, string repo, string slug, PutRepositoryDocumentRequest body) =>
        putDocumentEndpoint.RunAsync(provider, owner, repo, slug, body, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<RepositoryDocumentVersionDto>>> ListRepositoryDocumentVersions(
        GitProvider provider, string owner, string repo, string slug) =>
        listVersionsEndpoint.RunAsync(provider, owner, repo, slug, HttpContext.RequestAborted);

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
