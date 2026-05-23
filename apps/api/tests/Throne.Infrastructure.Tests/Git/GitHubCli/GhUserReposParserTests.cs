using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhUserReposParserTests
{
    [Fact(DisplayName = "Parse распознаёт snake_case JSON от /user/repos")]
    public void Parses_full_snake_case_payload()
    {
        const string json = """
            [
              {
                "name": "Avtorus40Proj",
                "owner": {"login": "ACSXEXRA"},
                "default_branch": "master",
                "description": "Web-app",
                "private": true,
                "html_url": "https://github.com/ACSXEXRA/Avtorus40Proj"
              }
            ]
            """;

        var repos = GhUserReposParser.Parse(json);

        repos.Should().ContainSingle();
        var repo = repos[0];
        repo.Provider.Should().Be("github");
        repo.Owner.Should().Be("ACSXEXRA");
        repo.Repo.Should().Be("Avtorus40Proj");
        repo.DefaultBranch.Should().Be("master");
        repo.Description.Should().Be("Web-app");
        repo.Private.Should().BeTrue();
        repo.HtmlUrl.Should().Be("https://github.com/ACSXEXRA/Avtorus40Proj");
    }

    [Fact(DisplayName = "Parse пропускает элементы без default_branch")]
    public void Skips_items_missing_default_branch()
    {
        const string json = """
            [{"name":"missing-branch","owner":{"login":"alice"}}]
            """;

        GhUserReposParser.Parse(json).Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse возвращает пустой список на пустой строке")]
    public void Empty_payload_yields_empty()
    {
        GhUserReposParser.Parse(string.Empty).Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse падает FormatException для объекта вместо массива")]
    public void Non_array_throws()
    {
        var act = () => GhUserReposParser.Parse("{}");

        act.Should().Throw<FormatException>();
    }
}
