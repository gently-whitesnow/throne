using Throne.Application.Ports;

namespace Throne.Application.Auth;

/// <summary>
/// Application-уровневая реализация <see cref="IPersonalAccessTokenResolver"/>:
/// считает SHA-256 plaintext-токена и ищет PAT в репозитории по hash.
/// Сравнение plaintext не нужно — поиск делается ровно по одному токену в БД,
/// а sha256-hash является криптографическим коммитментом.
/// </summary>
public sealed class PersonalAccessTokenResolver(IPersonalAccessTokenRepository repository)
    : IPersonalAccessTokenResolver
{
    public async Task<string?> ResolveOwnerUserIdAsync(string plaintextToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(plaintextToken))
        {
            return null;
        }

        if (!plaintextToken.StartsWith(PersonalAccessTokenSecretFactory.Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var hash = PersonalAccessTokenSecretFactory.ComputeSha256Hex(plaintextToken);
        var token = await repository.FindByHashAsync(hash, ct).ConfigureAwait(false);
        return token?.OwnerUserId;
    }
}
