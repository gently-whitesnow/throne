using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Intents;

public class MoveIntentHandlerTests
{
    [Fact(DisplayName = "MoveIntent без before_id и after_id отдаёт validation_failed (422)")]
    public async Task Move_without_pivots_returns_validation_failed()
    {
        var repo = Substitute.For<IIntentRepository>();
        var handler = new MoveIntentHandler(repo, new PassthroughUnitOfWork());

        var act = () => handler.HandleAsync(
            new MoveIntentCommand(IntentId.New().Value, BeforeId: null, AfterId: null),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        await repo.DidNotReceiveWithAnyArgs().MoveBetweenAsync(default, default, default, default);
    }

    // Регрессия: на проде фронт сортировал ключи через localeCompare (case-insensitive),
    // получались инвертированные пивоты ('s' слева, 'V' справа). FractionalIndex.Between
    // справедливо бросал ArgumentException, но Kestrel конвертировал её в 500. Хендлер
    // должен ловить такие случаи и отдавать 422, чтобы клиент сделал refetch вместо
    // алёрта про серверную ошибку.
    [Fact(DisplayName = "MoveIntent при инвертированных пивотах конвертирует ArgumentException в validation_failed")]
    public async Task Move_with_inverted_pivots_returns_validation_failed()
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.MoveBetweenAsync(Arg.Any<IntentId>(), Arg.Any<IntentId?>(), Arg.Any<IntentId?>(), Arg.Any<CancellationToken>())
            .Returns<Task<MoveIntentOutcome>>(_ =>
                throw new ArgumentException("before ('s') must be lexicographically less than after ('V').", "before"));

        var handler = new MoveIntentHandler(repo, new PassthroughUnitOfWork());

        var act = () => handler.HandleAsync(
            new MoveIntentCommand(IntentId.New().Value, BeforeId: "before-id", AfterId: "after-id"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Detail.Should().Contain("Pivots are out of order");
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
