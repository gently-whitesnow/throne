using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Throne.Application.Auth;

namespace Throne.Application.Tests.Auth;

public class PersonalAccessTokenSecretFactoryTests
{
    [Fact(DisplayName = "Issue выдаёт plaintext с префиксом tpat_ и непустым телом")]
    public void Issue_returns_prefixed_plaintext()
    {
        var factory = new PersonalAccessTokenSecretFactory();

        var secret = factory.Issue();

        secret.Plaintext.Should().StartWith(PersonalAccessTokenSecretFactory.Prefix);
        secret.Plaintext.Length.Should().BeGreaterThan(PersonalAccessTokenSecretFactory.Prefix.Length + 16);
    }

    [Fact(DisplayName = "Issue считает SHA-256 хеш plaintext-а в hex-форме")]
    public void Issue_computes_sha256_hash()
    {
        var factory = new PersonalAccessTokenSecretFactory();

        var secret = factory.Issue();

        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Plaintext)));
        secret.HashSha256.Should().Be(expected);
        secret.HashSha256.Length.Should().Be(64);
    }

    [Fact(DisplayName = "Issue возвращает последние 4 символа plaintext в LastFour")]
    public void Issue_returns_last_four_chars()
    {
        var factory = new PersonalAccessTokenSecretFactory();

        var secret = factory.Issue();

        secret.LastFour.Should().HaveLength(4);
        secret.Plaintext.Should().EndWith(secret.LastFour);
    }

    [Fact(DisplayName = "Issue выдаёт уникальные plaintext-ы при повторных вызовах")]
    public void Issue_generates_distinct_secrets()
    {
        var factory = new PersonalAccessTokenSecretFactory();

        var a = factory.Issue();
        var b = factory.Issue();

        a.Plaintext.Should().NotBe(b.Plaintext);
        a.HashSha256.Should().NotBe(b.HashSha256);
    }

    [Fact(DisplayName = "ComputeSha256Hex детерминированно считает hex-хеш")]
    public void ComputeSha256Hex_is_deterministic()
    {
        var hash1 = PersonalAccessTokenSecretFactory.ComputeSha256Hex("tpat_example");
        var hash2 = PersonalAccessTokenSecretFactory.ComputeSha256Hex("tpat_example");

        hash1.Should().Be(hash2);
        hash1.Length.Should().Be(64);
    }
}
