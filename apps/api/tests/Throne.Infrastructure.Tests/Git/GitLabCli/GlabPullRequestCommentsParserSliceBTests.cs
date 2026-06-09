using FluentAssertions;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitLabCli;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

/// <summary>
/// Slice B enrichment in <see cref="GlabPullRequestCommentsParser"/>: thread_id /
/// resolved from the discussion, and line / side from <c>position</c>.
/// </summary>
public class GlabPullRequestCommentsParserSliceBTests
{
    [Fact(DisplayName = "Parser читает thread_id/resolved/line/side для resolvable note (new_line)")]
    public void Parser_reads_resolvable_note_with_new_line()
    {
        const string json = """
            [{"id":"disc-1","notes":[
              {"id":101,"system":false,"author":{"username":"r"},"body":"x",
               "created_at":"2026-06-07T10:00:00Z","resolvable":true,"resolved":true,
               "position":{"new_path":"src/Foo.cs","new_line":42}}]}]
            """;

        var comment = GlabPullRequestCommentsParser.Parse(json).Should().ContainSingle().Subject;

        comment.ThreadId.Should().Be("disc-1");
        comment.Resolved.Should().BeTrue();
        comment.Line.Should().Be(42);
        comment.Side.Should().Be(ReviewCommentSide.Right);
        comment.Path.Should().Be("src/Foo.cs");
    }

    [Fact(DisplayName = "Parser мапит old_line в Side=Left")]
    public void Parser_maps_old_line_to_left()
    {
        const string json = """
            [{"id":"disc-2","notes":[
              {"id":102,"system":false,"author":{"username":"r"},"body":"x",
               "created_at":"2026-06-07T10:00:00Z","resolvable":true,"resolved":false,
               "position":{"old_path":"src/Foo.cs","old_line":7}}]}]
            """;

        var comment = GlabPullRequestCommentsParser.Parse(json).Should().ContainSingle().Subject;

        comment.Line.Should().Be(7);
        comment.Side.Should().Be(ReviewCommentSide.Left);
        comment.Resolved.Should().BeFalse();
    }

    [Fact(DisplayName = "Parser оставляет resolved/thread_id null для non-resolvable note")]
    public void Parser_leaves_resolution_null_for_non_resolvable_note()
    {
        const string json = """
            [{"id":"disc-3","notes":[
              {"id":103,"system":false,"author":{"username":"r"},"body":"x",
               "created_at":"2026-06-07T10:00:00Z","resolvable":false}]}]
            """;

        var comment = GlabPullRequestCommentsParser.Parse(json).Should().ContainSingle().Subject;

        comment.Resolved.Should().BeNull();
        comment.ThreadId.Should().BeNull();
        comment.Line.Should().BeNull();
        comment.Side.Should().BeNull();
    }
}
