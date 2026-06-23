using FluentAssertions;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Tags;

namespace Throne.Application.Tests.Terminals;

public class RunPreflightTagNamesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact(DisplayName = "Резолвит id → имена в порядке, пропуская id без тега")]
    public async Task Resolves_ids_to_names_skipping_missing()
    {
        var present = TagId.New();
        var missing = TagId.New();
        var repo = Substitute.For<ITagRepository>();
        repo.GetByIdAsync(present, Arg.Any<CancellationToken>())
            .Returns(Tag.Create(present, "throne", Now));
        repo.GetByIdAsync(missing, Arg.Any<CancellationToken>())
            .Returns((Tag?)null);

        var names = await new RunPreflightTagNames(repo).ResolveAsync([present, missing], CancellationToken.None);

        names.Should().Equal("throne");
    }

    [Fact(DisplayName = "Пустой список тегов → пустой результат, без обращений к репозиторию")]
    public async Task Empty_tag_ids_short_circuit()
    {
        var repo = Substitute.For<ITagRepository>();

        var names = await new RunPreflightTagNames(repo).ResolveAsync([], CancellationToken.None);

        names.Should().BeEmpty();
        await repo.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
    }
}
