using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Throne.Infrastructure.EfCore;
using Throne.Infrastructure.Security;

namespace Throne.Infrastructure.Tests.Security;

public sealed class LocalKeySecretProtectorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"throne-secret-{Guid.NewGuid():N}");

    [Fact(DisplayName = "Protect/Unprotect round-trips the secret and ciphertext is not the plaintext")]
    public void RoundTrips()
    {
        var protector = Create();
        const string token = "kaiten-pat-7f3c9";

        var protectedValue = protector.Protect(token);

        protectedValue.Should().NotBe(token);
        protectedValue.Should().NotContain(token);
        protector.Unprotect(protectedValue).Should().Be(token);
    }

    [Fact(DisplayName = "A persisted key survives across protector instances (key file is reused)")]
    public void KeyIsPersisted()
    {
        var protectedValue = Create().Protect("secret");
        Create().Unprotect(protectedValue).Should().Be("secret");
        File.Exists(Path.Combine(_dir, "secrets.key")).Should().BeTrue();
    }

    [Fact(DisplayName = "A tampered envelope fails authentication instead of decrypting to garbage")]
    public void TamperIsRejected()
    {
        var protector = Create();
        var bytes = Convert.FromBase64String(protector.Protect("secret"));
        bytes[^1] ^= 0xFF;

        var act = () => protector.Unprotect(Convert.ToBase64String(bytes));

        act.Should().Throw<CryptographicException>();
    }

    private LocalKeySecretProtector Create()
    {
        var options = Options.Create(new EfPersistenceOptions { DataSource = Path.Combine(_dir, "throne.db") });
        return new LocalKeySecretProtector(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
