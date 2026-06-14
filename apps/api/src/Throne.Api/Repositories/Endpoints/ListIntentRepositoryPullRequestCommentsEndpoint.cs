using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Server is pointer-only: this endpoint proxies <c>gh api .../pulls/{n}/comments</c>
/// through the binding's provider and never reads from Mongo. The <c>since</c>
/// filter is honoured against the upstream-fresh feed; pagination is intentionally
/// absent.
/// </summary>
public sealed class ListIntentRepositoryPullRequestCommentsEndpoint(
    RepositoryBindingResolver resolver,
    ListPullRequestCommentsUseCase useCase
)
{
    public async Task<ActionResult<ICollection<PullRequestCommentDto>>> RunAsync(
        string intentId,
        string bindingId,
        DateTimeOffset? since,
        CancellationToken ct
    )
    {
        await resolver.EnsureIntentExistsAsync(intentId, ct);
        var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
        var comments = await useCase.ListByBindingAsync(binding, ct);
        var filtered = since is null
            ? comments
            : comments.Where(c => c.CreatedAt >= since.Value).ToList();
        var dtos = filtered
            .OrderBy(c => c.CreatedAt)
            .Select(c => RepositoryDtoMapper.ToCommentDto(c, binding.Id))
            .ToList();
        return new OkObjectResult(dtos);
    }
}
