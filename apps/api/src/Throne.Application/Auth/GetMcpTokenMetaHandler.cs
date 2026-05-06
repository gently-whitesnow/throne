using Throne.Application.Ports;

namespace Throne.Application.Auth;

public sealed record McpTokenMeta(bool HasToken, DateTimeOffset? CreatedAt, string? LastFour);

/// <summary>
/// Возвращает метаинформацию о PAT текущего пользователя без plaintext-секрета.
/// Если токена нет — <c>HasToken=false</c>.
/// </summary>
public sealed class GetMcpTokenMetaHandler(
    IPersonalAccessTokenRepository repository,
    ICurrentUserAccessor currentUser)
{
    public async Task<McpTokenMeta> HandleAsync(CancellationToken ct)
    {
        var token = await repository.FindByOwnerAsync(currentUser.UserId, ct);
        return token is null
            ? new McpTokenMeta(HasToken: false, CreatedAt: null, LastFour: null)
            : new McpTokenMeta(HasToken: true, CreatedAt: token.CreatedAt, LastFour: token.LastFour);
    }
}
