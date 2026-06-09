using FluentAssertions;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhPullRequestDiffParserTests
{
    [Fact(DisplayName = "Parse читает /pulls/{n}/files и маппит status")]
    public void Parse_reads_files_payload()
    {
        const string json = """
            [{"filename":"src/a.cs","status":"modified","patch":"@@ -1 +1 @@\n-old\n+new"},
             {"filename":"src/b.cs","previous_filename":"src/old.cs","status":"renamed","patch":""},
             {"filename":"docs/c.md","status":"added","patch":"@@ -0,0 +1 @@\n+hi"}]
            """;

        var files = GhPullRequestDiffParser.Parse(json);

        files.Should().HaveCount(3);
        files[0].Path.Should().Be("src/a.cs");
        files[0].Status.Should().Be(PullRequestDiffFileStatus.Modified);
        files[0].Patch.Should().Contain("+new");
        files[1].PreviousPath.Should().Be("src/old.cs");
        files[1].Status.Should().Be(PullRequestDiffFileStatus.Renamed);
        files[2].Status.Should().Be(PullRequestDiffFileStatus.Added);
    }

    [Fact(DisplayName = "Parse возвращает пустой список на пустой body")]
    public void Parse_returns_empty_on_empty_body()
    {
        GhPullRequestDiffParser.Parse(string.Empty).Should().BeEmpty();
    }

    [Fact(DisplayName = "Parse бросает FormatException на не-массив")]
    public void Parse_throws_on_non_array_payload()
    {
        var act = () => GhPullRequestDiffParser.Parse("""{"foo":1}""");
        act.Should().Throw<FormatException>();
    }

    [Fact(DisplayName = "Parse склеивает многостраничный --paginate ответ (>100 файлов)")]
    public void Parse_reads_multipage_payload()
    {
        // gh --paginate concatenates pages as [..][..] with no delimiter.
        var page1 = Page(start: 0, count: 100);
        var page2 = Page(start: 100, count: 50);

        var files = GhPullRequestDiffParser.Parse(page1 + page2);

        files.Should().HaveCount(150);
        files[0].Path.Should().Be("f0.cs");
        files[^1].Path.Should().Be("f149.cs");
    }

    private static string Page(int start, int count)
    {
        var items = Enumerable.Range(start, count)
            .Select(i => $$"""{"filename":"f{{i}}.cs","status":"modified","patch":"@@ -1 +1 @@\n-a\n+b"}""");
        return "[" + string.Join(",", items) + "]";
    }
}
