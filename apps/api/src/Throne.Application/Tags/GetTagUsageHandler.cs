using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

public sealed record GetTagUsageQuery(string TagId);

public sealed class GetTagUsageHandler(ITagRepository repository)
{
    public async Task<TagUsage> HandleAsync(GetTagUsageQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var id = new TagId(query.TagId);
        _ = await repository.GetByIdAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.TagNotFound,
                $"Tag '{query.TagId}' not found.",
                new Dictionary<string, object?> { ["tag_id"] = query.TagId });

        return await repository.GetUsageAsync(id, ct);
    }
}
