using Microsoft.AspNetCore.Mvc;
using Throne.Api.Repositories.Endpoints;
using Throne.Repositories.Contracts.Generated;
using ReviewDiffScope = Throne.Application.Repositories.ReviewDiffScope;

namespace Throne.Api.Repositories;

public sealed class ReviewWorkspaceReadEndpoints(
    GetIntentRepositoryReviewDiffEndpoint diffEndpoint,
    GetIntentRepositoryReviewFileLinesEndpoint fileLinesEndpoint)
{
    public Task<ActionResult<PullRequestDiffDto>> GetDiffAsync(
        string intentId,
        string bindingId,
        ReviewDiffScope? scope,
        string? commitSha,
        CancellationToken ct) =>
        diffEndpoint.RunAsync(intentId, bindingId, scope, commitSha, ct);

    public Task<ActionResult<ReviewFileLinesDto>> GetFileLinesAsync(
        string intentId,
        string bindingId,
        string sha,
        string path,
        int from,
        int to,
        CancellationToken ct) =>
        fileLinesEndpoint.RunAsync(intentId, bindingId, sha, path, from, to, ct);
}
