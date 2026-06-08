using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Direct coverage of <see cref="GhPullRequestCommentsParser"/>. The
/// end-to-end happy path lives in
/// <see cref="GitHubCliProviderPullRequestCommentsTests"/>; these tests pin the
/// defensive parser behaviour (empty body, missing required fields) at the
/// component level so the polling service can trust the contract.
/// </summary>
public class GhPullRequestCommentsParserTests
{
    [Fact(DisplayName = "Parse возвращает пустой список на пустой body")]
    public void Parse_returns_empty_on_empty_body()
    {
        var parsed = GhPullRequestCommentsParser.Parse(string.Empty);

        parsed.Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse бросает FormatException на не-массив")]
    public void Parse_throws_on_non_array_payload()
    {
        var act = () => GhPullRequestCommentsParser.Parse("""{"items":[]}""");

        act.Should().Throw<FormatException>();
    }

    [Fact(DisplayName = "Parse дропает запись без user.login или created_at")]
    public void Parse_drops_items_missing_required_fields()
    {
        const string json = """
            [{"id":1,"user":{"login":"a"},"created_at":"2026-05-23T10:00:00Z","body":"ok"},
             {"id":2,"user":{},"created_at":"2026-05-23T10:00:00Z","body":"no login"},
             {"id":3,"user":{"login":"c"},"body":"no created_at"}]
            """;

        var parsed = GhPullRequestCommentsParser.Parse(json);

        parsed.Should().ContainSingle().Which.AuthorLogin.Should().Be("a");
    }

    [Fact(DisplayName = "Parse читает issue-comment JSON без path/position")]
    public void Parse_handles_issue_comment_payload_without_path()
    {
        // The discussion-thread endpoint returns the same envelope minus the
        // diff-anchored fields; Path must surface as null (not throw).
        const string json = """
            [{"id":42,"user":{"login":"reporter","avatar_url":"https://gh/u.png"},
              "body":"plain discussion","created_at":"2026-05-23T09:30:00Z",
              "updated_at":"2026-05-23T09:31:00Z",
              "html_url":"https://gh/x/y/issues/7#c42"}]
            """;

        var parsed = GhPullRequestCommentsParser.Parse(json);

        var item = parsed.Should().ContainSingle().Subject;
        item.Id.Should().Be("42");
        item.AuthorLogin.Should().Be("reporter");
        item.Path.Should().BeNull();
        item.HtmlUrl.Should().Contain("issues/7");
    }
}
