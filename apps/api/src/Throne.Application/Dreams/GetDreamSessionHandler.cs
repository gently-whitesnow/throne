using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Dreams;

namespace Throne.Application.Dreams;

/// <summary>
/// Owner-scoped read of a single <see cref="DreamSession"/>. Throws
/// <see cref="ApiException"/> with code <c>dream_session.not_found</c> when the
/// id is unknown or belongs to a different owner.
/// </summary>
public sealed class GetDreamSessionHandler(IDreamSessionRepository sessions)
{
    public async Task<DreamSession> HandleAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var session = await sessions.GetAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.DreamSessionNotFound,
                $"DreamSession '{id}' not found.",
                new Dictionary<string, object?> { ["dream_session_id"] = id });
        return session;
    }
}
