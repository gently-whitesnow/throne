namespace Throne.Application.Auth;

/// <summary>
/// Резолвит plaintext PAT в <c>userId</c>. Используется MCP-middleware до того,
/// как HTTP-request получит ambient <c>ICurrentUserAccessor</c>: на момент вызова
/// пользователь ещё не аутентифицирован, поэтому реализация не зависит от
/// <c>ICurrentUserAccessor</c>.
/// </summary>
public interface IPersonalAccessTokenResolver
{
    Task<string?> ResolveOwnerUserIdAsync(string plaintextToken, CancellationToken ct);
}
