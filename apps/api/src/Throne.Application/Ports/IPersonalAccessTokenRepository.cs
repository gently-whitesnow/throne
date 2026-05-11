using Throne.Domain.Auth;

namespace Throne.Application.Ports;

/// <summary>
/// Хранилище Personal Access Token-ов. Один активный токен на пользователя:
/// <see cref="ReplaceForOwnerAsync"/> атомарно удаляет предыдущий и записывает новый.
/// </summary>
public interface IPersonalAccessTokenRepository
{
    /// <summary>
    /// Replace the current owner's token with a new one in a single transaction.
    /// Старый токен (если был) — удаляется.
    /// </summary>
    Task ReplaceForOwnerAsync(PersonalAccessToken token, CancellationToken ct);

    /// <summary>
    /// Метаданные текущего токена владельца — без plaintext-секрета.
    /// </summary>
    Task<PersonalAccessToken?> FindByOwnerAsync(string ownerUserId, CancellationToken ct);

    /// <summary>
    /// Поиск по SHA-256 хешу plaintext-секрета. Используется resolver-ом и работает
    /// в обход <c>ICurrentUserAccessor</c>: на момент вызова пользователь ещё не
    /// аутентифицирован.
    /// </summary>
    Task<PersonalAccessToken?> FindByHashAsync(string hashSha256, CancellationToken ct);
}
