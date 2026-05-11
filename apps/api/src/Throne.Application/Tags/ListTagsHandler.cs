using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

public sealed record ListTagsQuery;

public sealed class ListTagsHandler(ITagRepository repository)
{
    public Task<IReadOnlyList<Tag>> HandleAsync(ListTagsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return repository.ListAllAsync(ct);
    }
}
