using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhRepoListParserTests
{
    [Fact(DisplayName = "Parse распознаёт камелкейсный JSON от gh repo list")]
    public void Parses_full_camel_case_payload()
    {
        const string json = """
            [
              {
                "defaultBranchRef": {"name": "master"},
                "description": "Knowledge base",
                "isPrivate": false,
                "name": "throne",
                "nameWithOwner": "gently-whitesnow/throne",
                "owner": {"id": "u1", "login": "gently-whitesnow"},
                "url": "https://github.com/gently-whitesnow/throne"
              }
            ]
            """;

        var repos = GhRepoListParser.Parse(json);

        repos.Should().ContainSingle();
        var repo = repos[0];
        repo.Provider.Should().Be("github");
        repo.Owner.Should().Be("gently-whitesnow");
        repo.Repo.Should().Be("throne");
        repo.DefaultBranch.Should().Be("master");
        repo.Description.Should().Be("Knowledge base");
        repo.Private.Should().BeFalse();
        repo.HtmlUrl.Should().Be("https://github.com/gently-whitesnow/throne");
        repo.FullName.Should().Be("gently-whitesnow/throne");
    }

    [Fact(DisplayName = "Parse пропускает элементы без обязательных полей")]
    public void Skips_items_missing_required_fields()
    {
        const string json = """
            [
              {"name": "no-owner", "defaultBranchRef": {"name": "main"}},
              {"name": "no-branch", "owner": {"login": "alice"}},
              {"owner": {"login": "bob"}, "defaultBranchRef": {"name": "main"}}
            ]
            """;

        var repos = GhRepoListParser.Parse(json);

        repos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse возвращает пустой список для пустого payload")]
    public void Empty_input_yields_empty_list()
    {
        GhRepoListParser.Parse(string.Empty).Should().BeEmpty();
        GhRepoListParser.Parse("[]").Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse падает FormatException при не-массиве на верхнем уровне")]
    public void Non_array_root_throws_format_exception()
    {
        var act = () => GhRepoListParser.Parse("{\"name\":\"oops\"}");

        act.Should().Throw<FormatException>();
    }

    [Fact(DisplayName = "Parse выставляет Private=true когда isPrivate=true")]
    public void Reads_private_flag()
    {
        const string json = """
            [{"name":"secret","owner":{"login":"alice"},"defaultBranchRef":{"name":"main"},"isPrivate":true}]
            """;

        var repos = GhRepoListParser.Parse(json);

        repos.Single().Private.Should().BeTrue();
    }
}
