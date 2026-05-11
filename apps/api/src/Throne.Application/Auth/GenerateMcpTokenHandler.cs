using Throne.Application.Ports;
using Throne.Domain.Auth;

namespace Throne.Application.Auth;

public sealed record GenerateMcpTokenResult(
    string Plaintext,
    string LastFour,
    DateTimeOffset CreatedAt);

/// <summary>
/// Перевыпускает PAT текущего пользователя. Старый токен инвалидируется (репозиторий
/// удаляет предыдущий за одну транзакцию). Plaintext-секрет возвращается ровно один
/// раз — повторно его получить нельзя.
/// </summary>
public sealed class GenerateMcpTokenHandler(
    IPersonalAccessTokenRepository repository,
    PersonalAccessTokenSecretFactory secretFactory,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<GenerateMcpTokenResult> HandleAsync(CancellationToken ct)
    {
        var ownerUserId = currentUser.UserId;
        var secret = secretFactory.Issue();
        var now = clock.GetUtcNow();

        var token = PersonalAccessToken.Create(
            id: Guid.NewGuid().ToString("N"),
            ownerUserId: ownerUserId,
            hashSha256: secret.HashSha256,
            lastFour: secret.LastFour,
            createdAt: now);

        await repository.ReplaceForOwnerAsync(token, ct);

        return new GenerateMcpTokenResult(secret.Plaintext, secret.LastFour, now);
    }
}
