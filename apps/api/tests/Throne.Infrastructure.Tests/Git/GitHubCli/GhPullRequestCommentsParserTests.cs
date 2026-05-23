using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Direct coverage of <see cref="GhPullRequestCommentsParser"/>. The
/// end-to-end happy path lives in
/// <see cref="GitHubCliProviderPullRequestCommentsTests"/>; these tests pin the
/// defensive parser behaviour (empty body, missing required fields) at the
/// component level so the polling service (T-10) can trust the contract.
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
}
