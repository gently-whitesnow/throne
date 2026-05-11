using FluentAssertions;
using Throne.Domain.Auth;

namespace Throne.Domain.Tests.Auth;

public class PersonalAccessTokenTests
{
    private const string ValidHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact(DisplayName = "Create заполняет все поля и сохраняет CreatedAt")]
    public void Create_assigns_all_fields()
    {
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var token = PersonalAccessToken.Create(
            id: "id-1",
            ownerUserId: "user-42",
            hashSha256: ValidHash,
            lastFour: "abcd",
            createdAt: now);

        token.Id.Should().Be("id-1");
        token.OwnerUserId.Should().Be("user-42");
        token.HashSha256.Should().Be(ValidHash);
        token.LastFour.Should().Be("abcd");
        token.CreatedAt.Should().Be(now);
    }

    [Fact(DisplayName = "Create требует SHA-256 хеш ровно из 64 hex-символов")]
    public void Create_rejects_short_hash()
    {
        var act = () => PersonalAccessToken.Create(
            id: "id",
            ownerUserId: "user",
            hashSha256: "deadbeef",
            lastFour: "abcd",
            createdAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>().WithMessage("*hashSha256*");
    }

    [Fact(DisplayName = "Create требует ровно 4 символа в LastFour")]
    public void Create_rejects_bad_last_four()
    {
        var act = () => PersonalAccessToken.Create(
            id: "id",
            ownerUserId: "user",
            hashSha256: ValidHash,
            lastFour: "ab",
            createdAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>().WithMessage("*lastFour*");
    }

    [Fact(DisplayName = "Create требует непустой OwnerUserId")]
    public void Create_rejects_empty_owner()
    {
        var act = () => PersonalAccessToken.Create(
            id: "id",
            ownerUserId: " ",
            hashSha256: ValidHash,
            lastFour: "abcd",
            createdAt: DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
