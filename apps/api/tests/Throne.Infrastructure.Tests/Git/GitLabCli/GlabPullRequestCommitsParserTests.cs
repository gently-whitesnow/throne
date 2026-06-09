using FluentAssertions;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GlabPullRequestCommitsParserTests
{
    [Fact(DisplayName = "Parse читает commits MR JSON и берёт committed_date")]
    public void Parse_reads_commits_payload()
    {
        const string json = """
            [{"id":"aaa","message":"feat: x","author_name":"Alice",
              "authored_date":"2026-05-23T10:00:00Z","committed_date":"2026-05-23T10:00:30Z"},
             {"id":"bbb","message":"fix: y","author_name":"Bob",
              "authored_date":"2026-05-23T11:00:00Z"}]
            """;

        var commits = GlabPullRequestCommitsParser.Parse(json);

        commits.Should().HaveCount(2);
        commits[0].Sha.Should().Be("aaa");
        commits[0].AuthorLogin.Should().Be("Alice");
        commits[0].CommittedAt.Should().Be(new DateTimeOffset(2026, 5, 23, 10, 0, 30, TimeSpan.Zero));
        commits[1].CommittedAt.Should().Be(new DateTimeOffset(2026, 5, 23, 11, 0, 0, TimeSpan.Zero));
    }

    [Fact(DisplayName = "Parse склеивает многостраничный --paginate ответ (>100 коммитов)")]
    public void Parse_reads_multipage_payload()
    {
        // glab --paginate concatenates pages as [..][..] with no delimiter.
        var page1 = Page(start: 0, count: 100);
        var page2 = Page(start: 100, count: 25);

        var commits = GlabPullRequestCommitsParser.Parse(page1 + page2);

        commits.Should().HaveCount(125);
        commits[0].Sha.Should().Be("sha000");
        commits[^1].Sha.Should().Be("sha124");
    }

    private static string Page(int start, int count)
    {
        var items = Enumerable.Range(start, count)
            .Select(i => $$"""
                {"id":"sha{{i:000}}","message":"c{{i}}","author_name":"a",
                 "committed_date":"2026-05-23T10:00:00Z"}
                """);
        return "[" + string.Join(",", items) + "]";
    }
}
