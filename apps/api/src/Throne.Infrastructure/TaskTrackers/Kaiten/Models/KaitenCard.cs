namespace Throne.Infrastructure.TaskTrackers.Kaiten.Models;

/// <summary>
/// A Kaiten card — the unit Throne links an intent to. Carries the stable identity and the mutable
/// fields the sync axis needs to mirror and to detect change (<see cref="Updated"/> /
/// <see cref="ColumnChangedAt"/>, plus the monotonic <see cref="Version"/> Kaiten ticks on every
/// edit). The full Kaiten card has many more fields; this is the projection the adapter exposes,
/// extended as consumers need more.
/// </summary>
internal sealed record KaitenCard(
    long Id,
    string Title,
    string? Description,
    long BoardId,
    long ColumnId,
    long? LaneId,
    IReadOnlyList<long>? TagIds,
    int Condition,
    DateTimeOffset? Created,
    DateTimeOffset? Updated,
    DateTimeOffset? ColumnChangedAt,
    long? TypeId,
    int? Version = null);

/// <summary>Payload for <c>POST /cards</c>. Board/column place the card; lane/type are optional.</summary>
internal sealed record KaitenCreateCardRequest(
    string Title,
    long BoardId,
    long ColumnId,
    long? LaneId = null,
    string? Description = null,
    long? TypeId = null);

/// <summary>
/// Partial payload for <c>PATCH /cards/{id}</c>. Every field is optional; left-null fields are
/// omitted from the request (<see cref="KaitenJson"/>), so the same shape both edits content and
/// moves the card (set <see cref="ColumnId"/> / <see cref="LaneId"/>).
/// </summary>
internal sealed record KaitenUpdateCardRequest(
    string? Title = null,
    long? ColumnId = null,
    long? LaneId = null,
    string? Description = null,
    int? Condition = null,
    long? TypeId = null);

/// <summary>
/// Filters and pagination for <c>GET /cards</c>. All fields optional; only set ones reach the wire.
/// <see cref="Condition"/> follows Kaiten's encoding (1 = live, 2 = archived). <see cref="Query"/> is
/// Kaiten's full-text filter; <see cref="OrderBy"/>/<see cref="OrderDirection"/> pair (e.g. "updated"
/// + "desc") so the caller can ask for the recently-touched cards without pulling the whole board.
/// </summary>
internal sealed record KaitenCardQuery(
    long? BoardId = null,
    int? Condition = null,
    int? Limit = null,
    int? Offset = null,
    long? OwnerId = null,
    long? MemberId = null,
    string? Query = null,
    string? OrderBy = null,
    string? OrderDirection = null)
{
    public string ToQueryString()
    {
        var parts = new List<string>(9);
        if (BoardId is { } board)
        {
            parts.Add($"board_id={board}");
        }

        if (Condition is { } condition)
        {
            parts.Add($"condition={condition}");
        }

        if (Limit is { } limit)
        {
            parts.Add($"limit={limit}");
        }

        if (Offset is { } offset)
        {
            parts.Add($"offset={offset}");
        }

        if (OwnerId is { } owner)
        {
            parts.Add($"owner_id={owner}");
        }

        if (MemberId is { } member)
        {
            parts.Add($"member_id={member}");
        }

        if (!string.IsNullOrEmpty(Query))
        {
            parts.Add($"query={Uri.EscapeDataString(Query)}");
        }

        if (!string.IsNullOrEmpty(OrderBy))
        {
            parts.Add($"order_by={Uri.EscapeDataString(OrderBy)}");
        }

        if (!string.IsNullOrEmpty(OrderDirection))
        {
            parts.Add($"order_direction={Uri.EscapeDataString(OrderDirection)}");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
