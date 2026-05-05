using FluentAssertions;
using NSubstitute;
using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.Auth;

namespace Throne.Application.Tests.Auth;

public class PersonalAccessTokenResolverTests
{
    [Fact(DisplayName = "ResolveOwnerUserIdAsync возвращает OwnerUserId по валидному plaintext")]
    public async Task Resolves_known_token()
    {
        var plaintext = new PersonalAccessTokenSecretFactory().Issue().Plaintext;
        var hash = PersonalAccessTokenSecretFactory.ComputeSha256Hex(plaintext);
        var stored = PersonalAccessToken.Create(
            id: "id",
            ownerUserId: "user-9",
            hashSha256: hash,
            lastFour: plaintext[^4..],
            createdAt: DateTimeOffset.UtcNow);

        var repo = Substitute.For<IPersonalAccessTokenRepository>();
        repo.FindByHashAsync(hash, Arg.Any<CancellationToken>()).Returns(stored);

        var resolver = new PersonalAccessTokenResolver(repo);

        var result = await resolver.ResolveOwnerUserIdAsync(plaintext, CancellationToken.None);

        result.Should().Be("user-9");
    }

    [Fact(DisplayName = "ResolveOwnerUserIdAsync отказывает токену без префикса tpat_")]
    public async Task Rejects_token_without_prefix()
    {
        var repo = Substitute.For<IPersonalAccessTokenRepository>();
        var resolver = new PersonalAccessTokenResolver(repo);

        var result = await resolver.ResolveOwnerUserIdAsync("not-a-pat", CancellationToken.None);

        result.Should().BeNull();
        await repo.DidNotReceiveWithAnyArgs().FindByHashAsync(default!, default);
    }

    [Fact(DisplayName = "ResolveOwnerUserIdAsync возвращает null, если PAT неизвестен")]
    public async Task Returns_null_for_unknown_token()
    {
        var plaintext = "tpat_unknown";
        var repo = Substitute.For<IPersonalAccessTokenRepository>();
        repo.FindByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((PersonalAccessToken?)null);

        var resolver = new PersonalAccessTokenResolver(repo);

        var result = await resolver.ResolveOwnerUserIdAsync(plaintext, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "ResolveOwnerUserIdAsync игнорирует пустой токен")]
    public async Task Ignores_empty_token()
    {
        var repo = Substitute.For<IPersonalAccessTokenRepository>();
        var resolver = new PersonalAccessTokenResolver(repo);

        var result = await resolver.ResolveOwnerUserIdAsync(string.Empty, CancellationToken.None);

        result.Should().BeNull();
        await repo.DidNotReceiveWithAnyArgs().FindByHashAsync(default!, default);
    }
}
