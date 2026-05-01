using FluentAssertions;
using Throne.Domain.Intents;

namespace Throne.Domain.Tests.Intents;

public class IntentTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create задаёт current_version = 1 и timestamps")]
    public void Create_starts_at_version_1()
    {
        var intent = Intent.Create(IntentId.New(), "hello", tags: null, Now);

        intent.CurrentVersion.Should().Be(1);
        intent.CreatedAt.Should().Be(Now);
        intent.UpdatedAt.Should().Be(Now);
        intent.Tags.Should().BeEmpty();
    }

    [Fact(DisplayName = "Create нормализует tags: trim, dedup, выкидывает пустые")]
    public void Create_normalizes_tags()
    {
        var intent = Intent.Create(IntentId.New(), "x", ["throne", " throne ", "", "  ", "throne", "other"], Now);

        intent.Tags.Should().Equal("throne", "other");
    }

    [Fact(DisplayName = "Create отвергает пустой text")]
    public void Create_rejects_empty_text()
    {
        var act = () => Intent.Create(IntentId.New(), "", tags: null, Now);

        act.Should().Throw<ArgumentException>().WithParameterName("text");
    }

    [Fact(DisplayName = "Restore требует current_version >= 1")]
    public void Restore_rejects_zero_version()
    {
        var act = () => Intent.Restore(IntentId.New(), "x", currentVersion: 0, [], Now, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
