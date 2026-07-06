using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Intents;

public class SetIntentTitleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IIntentRepository _repo = Substitute.For<IIntentRepository>();

    private SetIntentTitleHandler Build() =>
        new(_repo, new PassthroughUnitOfWork(), new FixedClock(Now));

    [Fact(DisplayName = "Задаёт title через repository и возвращает интент")]
    public async Task Sets_title()
    {
        var intent = Intent.Create(new IntentId("intent-1"), "text", null, Now, title: "New");
        _repo.SetTitleAsync(Arg.Any<IntentId>(), 1, "New", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SetIntentTitleOutcome>(new SetIntentTitleOutcome.Updated(intent, Changed: true)));

        var result = await Build().HandleAsync(new SetIntentTitleCommand("intent-1", 1, "New"), CancellationToken.None);

        result.State.Title.Should().Be("New");
    }

    [Fact(DisplayName = "Очистка title разрешена")]
    public async Task Clears_title()
    {
        var intent = Intent.Create(new IntentId("intent-1"), "text", null, Now);
        _repo.SetTitleAsync(Arg.Any<IntentId>(), 1, null, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SetIntentTitleOutcome>(new SetIntentTitleOutcome.Updated(intent, Changed: true)));

        var result = await Build().HandleAsync(new SetIntentTitleCommand("intent-1", 1, null), CancellationToken.None);

        result.State.Title.Should().BeNull();
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
