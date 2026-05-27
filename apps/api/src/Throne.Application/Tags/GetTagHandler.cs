using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

public sealed record GetTagQuery(string TagId);

/// <summary>
/// Returns the <see cref="Tag"/> aggregate including <c>default_repositories</c> for
/// the Slice 2 <c>GET /api/v1/tags/{id}</c> surface. 404 on missing tag.
/// </summary>
public sealed class GetTagHandler(ITagRepository repository)
{
    public async Task<Tag> HandleAsync(GetTagQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await repository.GetByIdAsync(new TagId(query.TagId), ct)
            ?? throw new ApiException(
                ErrorCodes.TagNotFound,
                $"Tag '{query.TagId}' not found.",
                new Dictionary<string, object?> { ["tag_id"] = query.TagId });
    }
}
