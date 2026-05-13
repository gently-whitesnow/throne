using Throne.Application.Ports;

namespace Throne.Application.Dreams;

public sealed record ListDreamSessionsQuery(
    string? Vendor,
    string? Host,
    int? Limit,
    string? Cursor);

/// <summary>
/// Paginated list handler. Owner filtering happens inside the repository; this
/// handler clamps <c>limit</c> and forwards the optional vendor filter.
/// </summary>
public sealed class ListDreamSessionsHandler(IDreamSessionRepository sessions)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public Task<DreamSessionPage> HandleAsync(ListDreamSessionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = query.Limit ?? DefaultLimit;
        if (limit < 1)
        {
            limit = 1;
        }
        if (limit > MaxLimit)
        {
            limit = MaxLimit;
        }

        return sessions.ListAsync(
            new DreamSessionListFilter(NullIfEmpty(query.Vendor), NullIfEmpty(query.Host)),
            limit,
            query.Cursor,
            ct);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
