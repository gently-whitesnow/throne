using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhPullRequestListParserTests
{
    [Fact(DisplayName = "Parse распознаёт payload gh pr list --json и приводит state к нижнему регистру")]
    public void Parses_camel_case_payload()
    {
        const string json = """
            [
              {"number":42,"title":"fix: thing","headRefName":"fix/thing","state":"OPEN"}
            ]
            """;

        var prs = GhPullRequestListParser.Parse(json);

        prs.Should().ContainSingle();
        var pr = prs[0];
        pr.Number.Should().Be(42);
        pr.Title.Should().Be("fix: thing");
        pr.HeadRef.Should().Be("fix/thing");
        pr.State.Should().Be("open");
    }

    [Fact(DisplayName = "Parse пропускает элементы без обязательных полей")]
    public void Skips_items_missing_required_fields()
    {
        const string json = """
            [
              {"title":"no-number","headRefName":"x","state":"OPEN"},
              {"number":1,"title":"no-head","state":"OPEN"},
              {"number":2,"title":"no-state","headRefName":"x"}
            ]
            """;

        GhPullRequestListParser.Parse(json).Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse возвращает пустой список для пустого payload")]
    public void Empty_input_yields_empty_list()
    {
        GhPullRequestListParser.Parse(string.Empty).Should().BeEmpty();
        GhPullRequestListParser.Parse("[]").Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse падает FormatException при не-массиве на верхнем уровне")]
    public void Non_array_root_throws_format_exception()
    {
        var act = () => GhPullRequestListParser.Parse("{\"number\":1}");

        act.Should().Throw<FormatException>();
    }
}
