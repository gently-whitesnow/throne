using FluentAssertions;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GlabPullRequestDiffParserTests
{
    [Fact(DisplayName = "Parse читает /merge_requests/{iid}/diffs и маппит флаги")]
    public void Parse_reads_diffs_payload()
    {
        const string json = """
            [{"new_path":"src/a.cs","old_path":"src/a.cs","diff":"@@ -1 +1 @@\n-old\n+new",
              "new_file":false,"deleted_file":false,"renamed_file":false},
             {"new_path":"src/b.cs","old_path":"src/old.cs","diff":"",
              "renamed_file":true,"new_file":false,"deleted_file":false},
             {"new_path":"docs/c.md","old_path":"docs/c.md","diff":"@@ -0,0 +1 @@\n+hi",
              "new_file":true}]
            """;

        var files = GlabPullRequestDiffParser.Parse(json);

        files.Should().HaveCount(3);
        files[0].Path.Should().Be("src/a.cs");
        files[0].Status.Should().Be(PullRequestDiffFileStatus.Modified);
        files[1].PreviousPath.Should().Be("src/old.cs");
        files[1].Status.Should().Be(PullRequestDiffFileStatus.Renamed);
        files[2].Status.Should().Be(PullRequestDiffFileStatus.Added);
    }

    [Fact(DisplayName = "Parse возвращает пустой список на пустой body")]
    public void Parse_returns_empty_on_empty_body()
    {
        GlabPullRequestDiffParser.Parse(string.Empty).Should().BeEmpty();
    }
}
