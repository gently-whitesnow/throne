using FluentAssertions;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Direct coverage of <see cref="GhPullRequestParser"/> — state mapping is the
/// only branching logic worth pinning at parser level (open / closed / merged).
/// End-to-end behaviour through the provider lives in
/// <see cref="GitHubCliProviderPullRequestTests"/>.
/// </summary>
public class GhPullRequestParserTests
{
    [Theory(DisplayName = "Parse мапит state в PullRequestStateNames")]
    [InlineData("open", false, PullRequestStateNames.Open)]
    [InlineData("closed", false, PullRequestStateNames.Closed)]
    [InlineData("closed", true, PullRequestStateNames.Merged)]
    [InlineData("open", true, PullRequestStateNames.Merged)]
    public void Parse_maps_state(string raw, bool merged, string expected)
    {
        var json = $$"""{"number":3,"state":"{{raw}}","merged":{{(merged ? "true" : "false")}}}""";

        var snapshot = GhPullRequestParser.Parse(json, requestedNumber: 3);

        snapshot.Should().NotBeNull();
        snapshot!.State.Should().Be(expected);
    }

    [Fact(DisplayName = "Parse подставляет requestedNumber при отсутствии number")]
    public void Parse_falls_back_to_requested_number()
    {
        const string json = """{"state":"open","merged":false}""";

        var snapshot = GhPullRequestParser.Parse(json, requestedNumber: 99);

        snapshot!.Number.Should().Be(99);
    }

    [Fact(DisplayName = "Parse вынимает тело, автора и ветки PR-шапки")]
    public void Parse_extracts_header_fields()
    {
        const string json = """
        {
          "number": 7,
          "state": "open",
          "merged": false,
          "title": "Add description tab",
          "html_url": "https://github.com/o/r/pull/7",
          "body": "# Why\nContext here.",
          "user": { "login": "octocat", "avatar_url": "https://avatars/octocat.png" },
          "head": { "ref": "feat/desc" },
          "base": { "ref": "main" }
        }
        """;

        var snapshot = GhPullRequestParser.Parse(json, requestedNumber: 7);

        snapshot.Should().NotBeNull();
        snapshot!.Body.Should().Be("# Why\nContext here.");
        snapshot.AuthorLogin.Should().Be("octocat");
        snapshot.AuthorAvatarUrl.Should().Be("https://avatars/octocat.png");
        snapshot.HeadRef.Should().Be("feat/desc");
        snapshot.BaseRef.Should().Be("main");
    }
}
