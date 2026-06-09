using System.Text.Json;
using FluentAssertions;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Pins the Slice B enrichment in <see cref="GhPullRequestCommentProjector"/>:
/// the review endpoint carries <c>line</c>/<c>side</c>, the issue endpoint does
/// not, and <c>original_line</c> is the fallback once <c>line</c> goes null.
/// </summary>
public class GhPullRequestCommentProjectorTests
{
    [Fact(DisplayName = "Projector читает line и side с review-комментария")]
    public void Projector_reads_line_and_side()
    {
        var parsed = ParseSingle("""
            [{"id":101,"user":{"login":"a"},"created_at":"2026-05-23T10:00:00Z",
              "body":"x","path":"src/Foo.cs","line":42,"side":"RIGHT"}]
            """);

        parsed.Line.Should().Be(42);
        parsed.Side.Should().Be(ReviewCommentSide.Right);
    }

    [Fact(DisplayName = "Projector мапит side=LEFT в ReviewCommentSide.Left")]
    public void Projector_maps_left_side()
    {
        var parsed = ParseSingle("""
            [{"id":101,"user":{"login":"a"},"created_at":"2026-05-23T10:00:00Z",
              "body":"x","line":7,"side":"LEFT"}]
            """);

        parsed.Side.Should().Be(ReviewCommentSide.Left);
    }

    [Fact(DisplayName = "Projector берёт original_line когда line отсутствует")]
    public void Projector_falls_back_to_original_line()
    {
        var parsed = ParseSingle("""
            [{"id":101,"user":{"login":"a"},"created_at":"2026-05-23T10:00:00Z",
              "body":"x","line":null,"original_line":99,"side":"RIGHT"}]
            """);

        parsed.Line.Should().Be(99);
    }

    [Fact(DisplayName = "Projector оставляет line и side null для issue-комментария")]
    public void Projector_leaves_line_and_side_null_for_issue_comment()
    {
        var parsed = ParseSingle("""
            [{"id":42,"user":{"login":"a"},"created_at":"2026-05-23T10:00:00Z","body":"x"}]
            """);

        parsed.Line.Should().BeNull();
        parsed.Side.Should().BeNull();
    }

    private static PullRequestComment ParseSingle(string json) =>
        GhPullRequestCommentsParser.Parse(json).Should().ContainSingle().Subject;
}
