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
}
