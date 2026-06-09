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
}
