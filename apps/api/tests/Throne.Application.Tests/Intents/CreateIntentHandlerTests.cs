using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Application.Tests.Intents;

public class CreateIntentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateIntent вставляет Intent и snapshot v1 через repository")]
    public async Task CreateIntent_persists_intent_and_v1_snapshot()
    {
        var repo = Substitute.For<IIntentRepository>();
        var uow = new PassthroughUnitOfWork();
        var clock = new FakeTimeProvider(Now);
        var handler = new CreateIntentHandler(repo, uow, clock);

        var intent = await handler.HandleAsync(new CreateIntentCommand("hello world", ["throne"], TextVersionAuthor.Agent), CancellationToken.None);

        intent.Text.Should().Be("hello world");
        intent.CurrentVersion.Should().Be(1);
        intent.Tags.Should().Equal("throne");
        intent.CreatedAt.Should().Be(Now);
        intent.UpdatedAt.Should().Be(Now);

        await repo.Received(1).CreateAsync(
            Arg.Is<Intent>(i => i.Text == "hello world" && i.CurrentVersion == 1),
            Arg.Is<TextVersion>(v =>
                v.Version == 1 &&
                v.Kind == TextVersionKind.Create &&
                v.OwnerKind == TextVersionOwnerKind.Intent &&
                v.Snapshot == "hello world"),
            Arg.Any<CancellationToken>());
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
