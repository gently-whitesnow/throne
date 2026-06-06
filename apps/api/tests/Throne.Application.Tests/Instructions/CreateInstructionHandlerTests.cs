using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Tests.Instructions;

public class CreateInstructionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Создание новой user-инструкции сохраняет её с текстом и version=1")]
    public async Task Creates_new_user_instruction()
    {
        var repo = Substitute.For<IInstructionRepository>();
        repo.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        Instruction? saved = null;
        repo.CreateAsync(Arg.Any<Instruction>(), Arg.Any<TextVersion>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved = call.Arg<Instruction>();
                return Task.CompletedTask;
            });
        var handler = NewHandler(repo);

        var result = await handler.HandleAsync(
            new CreateInstructionCommand(InstructionKindNames.Work, "hello"),
            CancellationToken.None);

        saved.Should().NotBeNull();
        saved!.Descriptor.Kind.Should().Be(InstructionKindNames.Work);
        saved.Text.Should().Be("hello");
        saved.CurrentVersion.Should().Be(1);
        result.Id.Value.Should().Be(saved.Id.Value);
    }

    [Fact(DisplayName = "Повторное создание уже существующей инструкции возвращает 409 instruction.already_exists")]
    public async Task Returns_409_when_already_exists()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var existing = Instruction.Create(
            InstructionId.New(), InstructionScopeNames.User, InstructionKindNames.Work, "x", Now);
        repo.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([existing]);
        var handler = NewHandler(repo);

        var act = async () => await handler.HandleAsync(
            new CreateInstructionCommand(InstructionKindNames.Work, "new"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.InstructionAlreadyExists);
        ex.Which.Extensions["kind"].Should().Be(InstructionKindNames.Work);
        ex.Which.Extensions["instruction_id"].Should().Be(existing.Id.Value);
        await repo.DidNotReceiveWithAnyArgs().CreateAsync(default!, default!, default);
    }

    [Fact(DisplayName = "Неизвестный kind отвергается validation.failed")]
    public async Task Rejects_unknown_kind()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var handler = NewHandler(repo);

        var act = async () => await handler.HandleAsync(
            new CreateInstructionCommand("bogus", "hello"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Extensions["field"].Should().Be("kind");
    }

    [Fact(DisplayName = "Пустой text допустим — создаёт v1 с пустым текстом")]
    public async Task Allows_empty_text()
    {
        var repo = Substitute.For<IInstructionRepository>();
        repo.GetUserInstructionsByKindsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        Instruction? saved = null;
        repo.CreateAsync(Arg.Any<Instruction>(), Arg.Any<TextVersion>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved = call.Arg<Instruction>();
                return Task.CompletedTask;
            });
        var handler = NewHandler(repo);

        await handler.HandleAsync(
            new CreateInstructionCommand(InstructionKindNames.Common, string.Empty),
            CancellationToken.None);

        saved!.Text.Should().Be(string.Empty);
        saved.CurrentVersion.Should().Be(1);
    }

    private static CreateInstructionHandler NewHandler(IInstructionRepository repo)
    {
        var uow = new PassThroughUnitOfWork();
        var clock = new FixedTimeProvider(Now);
        return new CreateInstructionHandler(repo, uow, clock);
    }

    private sealed class PassThroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
