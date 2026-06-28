using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.Intents;

public class SetIntentTitleHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly IIntentRepository _repo = Substitute.For<IIntentRepository>();
    private readonly ITaskTrackerCardLinkStore _links = Substitute.For<ITaskTrackerCardLinkStore>();

    private SetIntentTitleHandler Build() =>
        new(_repo, _links, new PassthroughUnitOfWork(), new FixedClock(Now));

    [Fact(DisplayName = "Задаёт title через repository и возвращает интент")]
    public async Task Sets_title()
    {
        var intent = Intent.Create(new IntentId("intent-1"), "text", null, Now, title: "New");
        _links.GetByIntentAsync("intent-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CardSyncLink?>(null));
        _repo.SetTitleAsync(Arg.Any<IntentId>(), 1, "New", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SetIntentTitleOutcome>(new SetIntentTitleOutcome.Updated(intent, Changed: true)));

        var result = await Build().HandleAsync(new SetIntentTitleCommand("intent-1", 1, "New"), CancellationToken.None);

        result.State.Title.Should().Be("New");
    }

    [Fact(DisplayName = "Очистка title на интенте без линка разрешена")]
    public async Task Clears_title_when_not_linked()
    {
        var intent = Intent.Create(new IntentId("intent-1"), "text", null, Now);
        _links.GetByIntentAsync("intent-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CardSyncLink?>(null));
        _repo.SetTitleAsync(Arg.Any<IntentId>(), 1, null, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<SetIntentTitleOutcome>(new SetIntentTitleOutcome.Updated(intent, Changed: true)));

        var result = await Build().HandleAsync(new SetIntentTitleCommand("intent-1", 1, null), CancellationToken.None);

        result.State.Title.Should().BeNull();
    }

    [Fact(DisplayName = "Очистка title на linked-интенте отвергается (422), repository не зовётся")]
    public async Task Clearing_title_on_linked_intent_is_rejected()
    {
        var link = CardSyncLink.Create(
            "intent-1", new TaskTrackerCardLink("kaiten", "board-7", "card-42"),
            new CardSyncLinkSnapshot("Title", "Body", null, null), CardSyncLinkCursors.Empty, Now);
        _links.GetByIntentAsync("intent-1", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CardSyncLink?>(link));

        var act = () => Build().HandleAsync(new SetIntentTitleCommand("intent-1", 1, "   "), CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        await _repo.DidNotReceive().SetTitleAsync(
            Arg.Any<IntentId>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
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
