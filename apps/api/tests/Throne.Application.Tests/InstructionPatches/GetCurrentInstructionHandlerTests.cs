using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.InstructionPatches;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Tests.InstructionPatches;

public class GetCurrentInstructionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "GetCurrentInstruction для существующей user-инструкции возвращает её текст и version")]
    public async Task Returns_existing_instruction()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var existing = InstructionFactory.Restore(
            InstructionId.New(),
            InstructionScopeNames.User,
            "user-1",
            InstructionKindNames.Work,
            "hello",
            currentVersion: 5,
            createdAt: Now,
            updatedAt: Now);
        repo.GetUserInstructionsByKindsAsync("user-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Instruction>>(new[] { existing }));
        var handler = new GetCurrentInstructionHandler(repo, new TestCurrentUserAccessor());

        var view = await handler.HandleAsync(InstructionKindNames.Work, CancellationToken.None);

        view.Kind.Should().Be(InstructionKindNames.Work);
        view.Text.Should().Be("hello");
        view.CurrentVersion.Should().Be(5);
        view.InstructionId.Should().Be(existing.Id.Value);
    }

    [Fact(DisplayName = "GetCurrentInstruction для отсутствующей записи возвращает синтетический stub version=0 (а не 404)")]
    public async Task Returns_stub_when_missing()
    {
        var repo = Substitute.For<IInstructionRepository>();
        repo.GetUserInstructionsByKindsAsync("user-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = new GetCurrentInstructionHandler(repo, new TestCurrentUserAccessor());

        var view = await handler.HandleAsync(InstructionKindNames.Work, CancellationToken.None);

        view.Kind.Should().Be(InstructionKindNames.Work);
        view.Text.Should().BeEmpty();
        view.CurrentVersion.Should().Be(0);
        view.InstructionId.Should().BeEmpty();
    }

    [Fact(DisplayName = "Неизвестный target_kind отвергается validation.failed")]
    public async Task Rejects_unknown_kind()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var handler = new GetCurrentInstructionHandler(repo, new TestCurrentUserAccessor());

        var act = async () => await handler.HandleAsync("bogus", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }
}
