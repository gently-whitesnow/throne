using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;

namespace Throne.Application.Tests.PromptPartPatches;

public class GetCurrentPromptPartHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "GetCurrentPromptPart для существующей части возвращает её текст и version")]
    public async Task Returns_existing_part()
    {
        var repo = Substitute.For<IPromptPartRepository>();
        var existing = PromptPart.Restore(
            PromptPartId.New(),
            PromptPartScopeNames.User,
            "work",
            "hello",
            description: null,
            currentVersion: 5,
            modeRoles: [],
            createdAt: Now,
            updatedAt: Now);
        repo.GetByScopeKeyAsync(PromptPartScopeNames.User, "work", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PromptPart?>(existing));
        var handler = new GetCurrentPromptPartHandler(new UserPromptPartLookup(repo));

        var view = await handler.HandleAsync(PromptPartScopeNames.User, "work", CancellationToken.None);

        view.Scope.Should().Be(PromptPartScopeNames.User);
        view.Key.Should().Be("work");
        view.Text.Should().Be("hello");
        view.CurrentVersion.Should().Be(5);
        view.PromptPartId.Should().Be(existing.Id.Value);
    }

    [Fact(DisplayName = "GetCurrentPromptPart для отсутствующей записи возвращает синтетический stub version=0")]
    public async Task Returns_stub_when_missing()
    {
        var repo = Substitute.For<IPromptPartRepository>();
        repo.GetByScopeKeyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<PromptPart?>(null));
        var handler = new GetCurrentPromptPartHandler(new UserPromptPartLookup(repo));

        var view = await handler.HandleAsync(PromptPartScopeNames.User, "work", CancellationToken.None);

        view.Key.Should().Be("work");
        view.Text.Should().BeEmpty();
        view.CurrentVersion.Should().Be(0);
        view.PromptPartId.Should().BeEmpty();
    }

    [Fact(DisplayName = "Неизвестный target_scope отвергается validation.failed")]
    public async Task Rejects_unknown_scope()
    {
        var repo = Substitute.For<IPromptPartRepository>();
        var handler = new GetCurrentPromptPartHandler(new UserPromptPartLookup(repo));

        var act = async () => await handler.HandleAsync("bogus", "work", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }
}
