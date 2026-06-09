using FluentAssertions;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GlabMrDiffRefsParserTests
{
    [Fact(DisplayName = "Parse поднимает base/head/start_sha из MR JSON")]
    public void Parse_reads_diff_refs()
    {
        const string json = """
            {"iid":7,"diff_refs":{"base_sha":"b","head_sha":"h","start_sha":"s"}}
            """;

        var refs = GlabMrDiffRefsParser.Parse(json);

        refs.Should().NotBeNull();
        refs!.Value.BaseSha.Should().Be("b");
        refs.Value.HeadSha.Should().Be("h");
        refs.Value.StartSha.Should().Be("s");
    }

    [Fact(DisplayName = "Parse возвращает null когда diff_refs отсутствует")]
    public void Parse_returns_null_without_diff_refs()
    {
        GlabMrDiffRefsParser.Parse("""{"iid":1}""").Should().BeNull();
    }
}
