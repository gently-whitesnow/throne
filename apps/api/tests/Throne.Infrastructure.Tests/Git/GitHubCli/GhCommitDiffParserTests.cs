using FluentAssertions;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhCommitDiffParserTests
{
    [Fact(DisplayName = "Parse читает commit JSON и поднимает parent.sha в base_sha")]
    public void Parse_lifts_parent_sha()
    {
        const string json = """
            {"sha":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
             "parents":[{"sha":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"}],
             "files":[{"filename":"x.cs","status":"modified","patch":"@@ -1 +1 @@\n-a\n+b"}]}
            """;

        var diff = GhCommitDiffParser.Parse(json, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

        diff.Should().NotBeNull();
        diff!.BaseSha.Should().StartWith("bbbb");
        diff.HeadSha.Should().StartWith("aaaa");
        diff.StartSha.Should().Be(diff.BaseSha);
        diff.Files.Should().ContainSingle().Which.Path.Should().Be("x.cs");
    }

    [Fact(DisplayName = "Parse возвращает null на пустой body")]
    public void Parse_returns_null_on_empty()
    {
        GhCommitDiffParser.Parse(string.Empty, "sha").Should().BeNull();
    }

    [Fact(DisplayName = "Parse работает без файлов")]
    public void Parse_handles_missing_files()
    {
        const string json = """{"sha":"a","parents":[{"sha":"b"}]}""";

        var diff = GhCommitDiffParser.Parse(json, "a");

        diff!.Files.Should().BeEmpty();
    }
}
