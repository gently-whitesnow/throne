using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhPullRequestCommitsParserTests
{
    [Fact(DisplayName = "Parse читает commits-of-pr JSON")]
    public void Parse_reads_commits_payload()
    {
        const string json = """
            [{"sha":"deadbeef0000000000000000000000000000beef","commit":{
                "message":"feat: add diff endpoint",
                "author":{"name":"Alice","date":"2026-05-23T10:00:00Z"},
                "committer":{"name":"Alice","date":"2026-05-23T10:00:30Z"}},
              "author":{"login":"alice"}},
             {"sha":"feedface0000000000000000000000000000face","commit":{
                "message":"fix: type",
                "author":{"name":"Bob","date":"2026-05-23T11:00:00Z"}}}]
            """;

        var commits = GhPullRequestCommitsParser.Parse(json);

        commits.Should().HaveCount(2);
        commits[0].Sha.Should().Be("deadbeef0000000000000000000000000000beef");
        commits[0].Message.Should().StartWith("feat:");
        commits[0].AuthorLogin.Should().Be("alice");
        commits[0].CommittedAt.Should().Be(new DateTimeOffset(2026, 5, 23, 10, 0, 30, TimeSpan.Zero));
        commits[1].AuthorLogin.Should().Be("Bob");
    }

    [Fact(DisplayName = "Parse возвращает пустой список на пустой body")]
    public void Parse_returns_empty_on_empty_body()
    {
        GhPullRequestCommitsParser.Parse(string.Empty).Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse склеивает многостраничный --paginate ответ (>100 коммитов)")]
    public void Parse_reads_multipage_payload()
    {
        // gh --paginate concatenates pages as [..][..] with no delimiter.
        var page1 = Page(start: 0, count: 100);
        var page2 = Page(start: 100, count: 30);

        var commits = GhPullRequestCommitsParser.Parse(page1 + page2);

        commits.Should().HaveCount(130);
        commits[0].Sha.Should().Be("sha000");
        commits[^1].Sha.Should().Be("sha129");
    }

    private static string Page(int start, int count)
    {
        var items = Enumerable.Range(start, count)
            .Select(i => "{\"sha\":\"sha" + i.ToString("000", System.Globalization.CultureInfo.InvariantCulture)
                + "\",\"commit\":{\"message\":\"c" + i
                + "\",\"author\":{\"name\":\"a\",\"date\":\"2026-05-23T10:00:00Z\"}}}");
        return "[" + string.Join(",", items) + "]";
    }
}
