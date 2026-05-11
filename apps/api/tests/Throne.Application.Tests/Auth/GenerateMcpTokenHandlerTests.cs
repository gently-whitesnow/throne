using FluentAssertions;
using NSubstitute;
using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.Auth;

namespace Throne.Application.Tests.Auth;

public class GenerateMcpTokenHandlerTests
{
    [Fact(DisplayName = "GenerateMcpTokenHandler сохраняет PAT с OwnerUserId из ICurrentUserAccessor")]
    public async Task Persists_token_with_current_user_owner()
    {
        var repo = Substitute.For<IPersonalAccessTokenRepository>();
        var clock = new FakeClock(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        var handler = new GenerateMcpTokenHandler(
            repo,
            new PersonalAccessTokenSecretFactory(),
            new TestCurrentUserAccessor("user-77"),
            clock);

        var result = await handler.HandleAsync(CancellationToken.None);

        await repo.Received(1).ReplaceForOwnerAsync(
            Arg.Is<PersonalAccessToken>(t => t.OwnerUserId == "user-77" && t.HashSha256.Length == 64),
            Arg.Any<CancellationToken>());

        result.Plaintext.Should().StartWith(PersonalAccessTokenSecretFactory.Prefix);
        result.LastFour.Should().HaveLength(4);
        result.CreatedAt.Should().Be(clock.GetUtcNow());
    }

    [Fact(DisplayName = "GenerateMcpTokenHandler возвращает plaintext один раз и хранит только хеш")]
    public async Task Returns_plaintext_once_and_persists_only_hash()
    {
        var captured = (PersonalAccessToken?)null;
        var repo = Substitute.For<IPersonalAccessTokenRepository>();
        repo.ReplaceForOwnerAsync(Arg.Do<PersonalAccessToken>(t => captured = t), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new GenerateMcpTokenHandler(
            repo,
            new PersonalAccessTokenSecretFactory(),
            new TestCurrentUserAccessor("user-1"),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await handler.HandleAsync(CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.HashSha256.Should().Be(PersonalAccessTokenSecretFactory.ComputeSha256Hex(result.Plaintext));
        captured.LastFour.Should().Be(result.LastFour);
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
