using FluentAssertions;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

/// <summary>
/// Direct coverage of <see cref="GlabPullRequestParser"/> — state normalisation
/// plus the PR-header fields (description / author / branches) read for the
/// review-workspace "Описание" tab.
/// </summary>
public class GlabPullRequestParserTests
{
    [Theory(DisplayName = "Parse нормализует state в PullRequestStateNames")]
    [InlineData("opened", PullRequestStateNames.Open)]
    [InlineData("closed", PullRequestStateNames.Closed)]
    [InlineData("merged", PullRequestStateNames.Merged)]
    public void Parse_normalizes_state(string raw, string expected)
    {
        var json = $$"""{"iid":3,"state":"{{raw}}"}""";

        var snapshot = GlabPullRequestParser.Parse(json, requestedNumber: 3);

        snapshot.Should().NotBeNull();
        snapshot!.State.Should().Be(expected);
    }

    [Fact(DisplayName = "Parse вынимает описание, автора и ветки MR-шапки")]
    public void Parse_extracts_header_fields()
    {
        const string json = """
        {
          "iid": 7,
          "state": "opened",
          "title": "Add description tab",
          "web_url": "https://gitlab.com/o/r/-/merge_requests/7",
          "description": "# Why\nContext here.",
          "author": { "username": "tanuki", "avatar_url": "https://avatars/tanuki.png" },
          "source_branch": "feat/desc",
          "target_branch": "main"
        }
        """;

        var snapshot = GlabPullRequestParser.Parse(json, requestedNumber: 7);

        snapshot.Should().NotBeNull();
        snapshot!.Number.Should().Be(7);
        snapshot.Body.Should().Be("# Why\nContext here.");
        snapshot.AuthorLogin.Should().Be("tanuki");
        snapshot.AuthorAvatarUrl.Should().Be("https://avatars/tanuki.png");
        snapshot.HeadRef.Should().Be("feat/desc");
        snapshot.BaseRef.Should().Be("main");
    }
}
