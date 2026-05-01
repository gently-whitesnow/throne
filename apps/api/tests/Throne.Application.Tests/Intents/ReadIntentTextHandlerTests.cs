using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Intents;

public class ReadIntentTextHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ReadIntentText возвращает диапазон строк с правильным end_line")]
    public async Task Read_returns_range()
    {
        var repo = Substitute.For<IIntentRepository>();
        var id = IntentId.New();
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(id, "a\nb\nc\nd\ne", 1, [], Now, Now));

        var handler = new ReadIntentTextHandler(repo);

        var slice = await handler.HandleAsync(new ReadIntentTextQuery(id.Value, StartLine: 2, LineCount: 2, MaxChars: null), CancellationToken.None);

        slice.StartLine.Should().Be(2);
        slice.EndLine.Should().Be(3);
        slice.TotalLines.Should().Be(5);
        slice.Content.Should().Be("b\nc");
        slice.Truncated.Should().BeFalse();
    }

    [Fact(DisplayName = "ReadIntentText помечает truncated и отдаёт next_start_line по max_chars")]
    public async Task Read_truncates_by_max_chars()
    {
        var repo = Substitute.For<IIntentRepository>();
        var id = IntentId.New();
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(id, "abc\ndef\nghi", 1, [], Now, Now));

        var handler = new ReadIntentTextHandler(repo);

        var slice = await handler.HandleAsync(new ReadIntentTextQuery(id.Value, StartLine: 1, LineCount: null, MaxChars: 4), CancellationToken.None);

        slice.Content.Should().Be("abc");
        slice.EndLine.Should().Be(1);
        slice.Truncated.Should().BeTrue();
        slice.NextStartLine.Should().Be(2);
    }
}
