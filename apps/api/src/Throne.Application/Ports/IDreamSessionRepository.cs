using Throne.Application.Dreams;
using Throne.Domain.Dreams;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="DreamSession"/>. Sessions are append-only;
/// there is intentionally no update or delete API.
/// </summary>
public interface IDreamSessionRepository
{
    Task<CreateDreamSessionOutcome> CreateAsync(DreamSession session, CancellationToken ct);

    Task<DreamSession?> GetAsync(string id, CancellationToken ct);

    /// <summary>
    /// Pagination order: descending by <c>created_at</c>; cursor is opaque
    /// (base64 of (created_at, id)).
    /// </summary>
    Task<DreamSessionPage> ListAsync(
        DreamSessionListFilter filter,
        int limit,
        string? cursor,
        CancellationToken ct);
}

/// <summary>
/// Optional filter set for <see cref="IDreamSessionRepository.ListAsync"/>.
/// Null fields mean «do not filter» on that dimension.
/// </summary>
public sealed record DreamSessionListFilter(string? Vendor, string? Host = null);

public sealed record DreamSessionPage(
    IReadOnlyList<DreamSession> Items,
    string? NextCursor);
