using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Tags;

/// <summary>Keyset cursor for the fixed <c>usage_count desc, _id asc</c> order.</summary>
public sealed record TagListCursor(int UsageCount, string Id);

/// <summary>A tag plus its denormalized usage count (intents currently referencing it).</summary>
public sealed record TagListItem(Tag Tag, int IntentsCount);

public sealed record TagListPage(IReadOnlyList<TagListItem> Items, TagListCursor? NextCursor);

public sealed record TagListSpec(string? Search, int Limit, TagListCursor? Cursor);

public sealed record ListTagsQuery(string? Search = null, int? Limit = null, TagListCursor? Cursor = null);

public sealed class ListTagsHandler(ITagRepository repository)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public Task<TagListPage> HandleAsync(ListTagsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = query.Limit is null or <= 0
            ? DefaultLimit
            : Math.Min(query.Limit.Value, MaxLimit);

        // Substring match runs against the stored (already lowercased) name, so a
        // lowercased trim of the raw search term is the normalized form here.
        var search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim().ToLowerInvariant();

        return repository.ListPageAsync(new TagListSpec(search, limit, query.Cursor), ct);
    }
}
