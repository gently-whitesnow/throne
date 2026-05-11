using System.Security.Cryptography;
using System.Text;

namespace Throne.Application.Auth;

/// <summary>
/// Криптографически стойкая фабрика PAT-секретов. 32 случайных байта → base64url
/// (без padding) → префикс <c>tpat_</c>; SHA-256 считается от plaintext-формы и
/// хранится в БД.
/// </summary>
public sealed class PersonalAccessTokenSecretFactory
{
    public const string Prefix = "tpat_";

    private const int RandomByteCount = 32;

    public PersonalAccessTokenSecret Issue()
    {
        Span<byte> bytes = stackalloc byte[RandomByteCount];
        RandomNumberGenerator.Fill(bytes);

        var random = Base64UrlEncode(bytes);
        var plaintext = Prefix + random;
        var hash = ComputeSha256Hex(plaintext);
        var lastFour = plaintext[^4..];
        return new PersonalAccessTokenSecret(plaintext, hash, lastFour);
    }

    public static string ComputeSha256Hex(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        var written = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext), hash);
        if (written != hash.Length)
        {
            throw new InvalidOperationException("SHA-256 produced unexpected hash size.");
        }

        return Convert.ToHexStringLower(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        var sb = new StringBuilder(base64.Length);
        foreach (var c in base64)
        {
            switch (c)
            {
                case '+':
                    sb.Append('-');
                    break;
                case '/':
                    sb.Append('_');
                    break;
                case '=':
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
