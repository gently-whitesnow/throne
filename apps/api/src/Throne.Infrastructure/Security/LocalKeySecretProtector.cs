using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Throne.Infrastructure.EfCore;

namespace Throne.Infrastructure.Security;

/// <summary>
/// AES-256-GCM <see cref="ISecretProtector"/> keyed by a 32-byte key persisted next to the SQLite
/// database (<c>secrets.key</c>, base64, <c>0600</c>). The key materialises on first use and is cached
/// in-process. The envelope is <c>base64(nonce[12] || tag[16] || ciphertext)</c> — AES-GCM is an
/// authenticated cipher, so a tampered or truncated value fails to decrypt rather than yielding
/// garbage.
/// </summary>
internal sealed class LocalKeySecretProtector : ISecretProtector
{
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const string KeyFileName = "secrets.key";

    private readonly string _keyPath;
    private readonly Lock _gate = new();
    private byte[]? _key;

    public LocalKeySecretProtector(IOptions<EfPersistenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var dbPath = options.Value.ResolveDataSourcePath();
        var directory = Path.GetDirectoryName(dbPath);
        _keyPath = string.IsNullOrEmpty(directory)
            ? KeyFileName
            : Path.Combine(directory, KeyFileName);
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var key = GetKey();
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);

        var envelope = new byte[NonceSize + TagSize + cipher.Length];
        nonce.CopyTo(envelope, 0);
        tag.CopyTo(envelope, NonceSize);
        cipher.CopyTo(envelope, NonceSize + TagSize);
        return Convert.ToBase64String(envelope);
    }

    public string Unprotect(string protectedValue)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);
        var key = GetKey();
        var envelope = Convert.FromBase64String(protectedValue);
        if (envelope.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Protected value is too short to be a valid envelope.");
        }

        var nonce = envelope.AsSpan(0, NonceSize);
        var tag = envelope.AsSpan(NonceSize, TagSize);
        var cipher = envelope.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    private byte[] GetKey()
    {
        lock (_gate)
        {
            return _key ??= LoadOrCreateKey();
        }
    }

    private byte[] LoadOrCreateKey()
    {
        if (File.Exists(_keyPath))
        {
            var existing = Convert.FromBase64String(File.ReadAllText(_keyPath).Trim());
            if (existing.Length == KeySize)
            {
                return existing;
            }
        }

        var fresh = RandomNumberGenerator.GetBytes(KeySize);
        var directory = Path.GetDirectoryName(_keyPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_keyPath, Convert.ToBase64String(fresh));
        RestrictToOwner(_keyPath);
        return fresh;
    }

    private static void RestrictToOwner(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
