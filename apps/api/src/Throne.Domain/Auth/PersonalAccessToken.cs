namespace Throne.Domain.Auth;

/// <summary>
/// Долгоживущий Personal Access Token (PAT) для MCP-клиентов. Хранится только хеш
/// SHA-256; <see cref="LastFour"/> используется UI для fingerprint без раскрытия секрета.
///
/// Доменные правила:
/// - один активный PAT на <see cref="OwnerUserId"/>;
/// - перегенерация = удалить старый + создать новый в одной транзакции
///   (контролирует репозиторий);
/// - TTL отсутствует (см. ADR-0016).
/// </summary>
public sealed class PersonalAccessToken
{
    private PersonalAccessToken(
        string id,
        string ownerUserId,
        string hashSha256,
        string lastFour,
        DateTimeOffset createdAt)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        HashSha256 = hashSha256;
        LastFour = lastFour;
        CreatedAt = createdAt;
    }

    public string Id { get; }
    public string OwnerUserId { get; }

    /// <summary>Hex-encoded SHA-256 hash of the plaintext secret.</summary>
    public string HashSha256 { get; }

    /// <summary>Last four characters of the plaintext secret (UI fingerprint, not sensitive).</summary>
    public string LastFour { get; }

    public DateTimeOffset CreatedAt { get; }

    public static PersonalAccessToken Create(
        string id,
        string ownerUserId,
        string hashSha256,
        string lastFour,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastFour);

        if (hashSha256.Length != 64)
        {
            throw new ArgumentException(
                "hashSha256 must be 64 hex characters (SHA-256).",
                nameof(hashSha256));
        }

        if (lastFour.Length != 4)
        {
            throw new ArgumentException(
                "lastFour must contain exactly 4 characters.",
                nameof(lastFour));
        }

        return new PersonalAccessToken(id, ownerUserId, hashSha256, lastFour, createdAt);
    }

    public static PersonalAccessToken Restore(
        string id,
        string ownerUserId,
        string hashSha256,
        string lastFour,
        DateTimeOffset createdAt)
        => Create(id, ownerUserId, hashSha256, lastFour, createdAt);
}
