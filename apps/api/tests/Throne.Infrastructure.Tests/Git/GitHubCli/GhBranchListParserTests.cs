using FluentAssertions;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhBranchListParserTests
{
    [Fact(DisplayName = "ParseDefault достаёт default_branch из payload repo metadata")]
    public void ParseDefault_reads_field()
    {
        const string json = """{"default_branch":"main","name":"hello","owner":{"login":"octo"}}""";

        GhBranchListParser.ParseDefault(json).Should().Be("main");
    }

    [Fact(DisplayName = "ParseDefault возвращает null для пустого / не-объектного payload")]
    public void ParseDefault_empty_or_non_object_returns_null()
    {
        GhBranchListParser.ParseDefault(string.Empty).Should().BeNull();
        GhBranchListParser.ParseDefault("[]").Should().BeNull();
    }

    [Fact(DisplayName = "ParseBranches маркирует default-ветку и пропускает пустые имена")]
    public void ParseBranches_marks_default_and_skips_empty()
    {
        const string json = """
            [
              {"name":"main","commit":{"sha":"a1"}},
              {"name":"feature/x","commit":{"sha":"b2"}},
              {"name":"","commit":{"sha":"c3"}}
            ]
            """;

        var branches = GhBranchListParser.ParseBranches(json, "main");

        branches.Should().HaveCount(2);
        branches[0].Name.Should().Be("main");
        branches[0].IsDefault.Should().BeTrue();
        branches[1].Name.Should().Be("feature/x");
        branches[1].IsDefault.Should().BeFalse();
    }

    [Fact(DisplayName = "ParseBranches падает FormatException на не-массиве")]
    public void ParseBranches_non_array_throws()
    {
        var act = () => GhBranchListParser.ParseBranches("{\"name\":\"x\"}", "main");

        act.Should().Throw<FormatException>();
    }
}
