using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Intents;

public enum IntentListSort
{
    /// <summary>User-defined order via fractional sort key, ascending (top of list = smallest key).</summary>
    SortKeyAsc,
    UpdatedDesc,
    CreatedDesc,
    CreatedAsc,
}

/// <summary>
/// Pagination cursor for time-based sorts (<see cref="IntentListSort.UpdatedDesc"/>,
/// <see cref="IntentListSort.CreatedDesc"/>, <see cref="IntentListSort.CreatedAsc"/>).
/// For <see cref="IntentListSort.SortKeyAsc"/> the cursor's <see cref="SortKey"/> is used instead.
/// </summary>
public sealed record IntentListCursor(DateTimeOffset SortValue, string Id, string? SortKey = null);

public sealed record ListIntentsQuery(IReadOnlyList<string>? Statuses = null);

public sealed record ListIntentsPagedQuery(
    IReadOnlyList<string>? Statuses = null,
    string? TagName = null,
    bool Untagged = false,
    bool Pinned = false,
    string? Query = null,
    IntentListSort Sort = IntentListSort.SortKeyAsc,
    int Limit = 50,
    IntentListCursor? Cursor = null);

public sealed record IntentListPage(IReadOnlyList<Intent> Items, IntentListCursor? NextCursor);

public sealed record IntentListSpec(
    IReadOnlyList<string>? Statuses,
    TagId? TagId,
    bool Untagged,
    bool Pinned,
    string? Query,
    IntentListSort Sort,
    int Limit,
    IntentListCursor? Cursor);

public sealed class ListIntentsHandler(IIntentRepository repository, ITagRepository tagRepository)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public async Task<IReadOnlyList<Intent>> HandleAsync(ListIntentsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await repository.ListAsync(query.Statuses, ct);
    }

    public async Task<IntentListPage> HandleAsync(ListIntentsPagedQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        TagId? tagId = null;
        if (!string.IsNullOrWhiteSpace(query.TagName))
        {
            var normalized = TagNames.Normalize(query.TagName);
            var tag = await tagRepository.FindByNameAsync(normalized, ct);
            if (tag is null)
            {
                return new IntentListPage([], NextCursor: null);
            }
            tagId = tag.Id;
        }

        var clampedLimit = query.Limit <= 0
            ? DefaultLimit
            : Math.Min(query.Limit, MaxLimit);

        var spec = new IntentListSpec(
            Statuses: query.Statuses,
            TagId: tagId,
            Untagged: query.Untagged,
            Pinned: query.Pinned,
            Query: string.IsNullOrWhiteSpace(query.Query) ? null : query.Query,
            Sort: query.Sort,
            Limit: clampedLimit,
            Cursor: query.Cursor);

        return await repository.ListPagedAsync(spec, ct);
    }
}
