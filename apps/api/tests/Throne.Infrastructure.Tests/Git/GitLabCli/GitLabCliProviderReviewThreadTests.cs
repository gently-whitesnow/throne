using FluentAssertions;
using Throne.Application.Git;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

/// <summary>
/// Slice B coverage for the GitLab resolve/delete review surface.
/// </summary>
public class GitLabCliProviderReviewThreadTests
{
    private readonly GitLabCliProviderFixture _fx = new();

    [Fact(DisplayName = "ResolveReviewThreadAsync шлёт PUT discussions/:id?resolved=true и читает resolved")]
    public async Task Resolve_puts_discussion_resolved_true()
    {
        const string body = """
            {"id":"disc-1","notes":[{"id":1,"resolvable":true,"resolved":true}]}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(body));

        var state = await _fx.Provider.ResolveReviewThreadAsync("g", "r", 7, "disc-1", resolved: true, default);

        state.ThreadId.Should().Be("disc-1");
        state.Resolved.Should().BeTrue();
        var call = _fx.Calls.Single();
        call.Arguments.Should().Contain("PUT");
        call.Arguments[^1].Should().Be("projects/g%2Fr/merge_requests/7/discussions/disc-1?resolved=true");
        GitLabCliProviderFixture.HasGitLabHost(call).Should().BeTrue();
    }

    [Fact(DisplayName = "ResolveReviewThreadAsync(false) шлёт resolved=false")]
    public async Task Resolve_puts_discussion_resolved_false()
    {
        const string body = """
            {"id":"disc-1","notes":[{"id":1,"resolvable":true,"resolved":false}]}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(body));

        var state = await _fx.Provider.ResolveReviewThreadAsync("g", "r", 7, "disc-1", resolved: false, default);

        state.Resolved.Should().BeFalse();
        _fx.Calls.Single().Arguments[^1].Should().EndWith("?resolved=false");
    }

    [Fact(DisplayName = "DeleteReviewCommentAsync шлёт DELETE discussions/:id/notes/:id")]
    public async Task Delete_issues_delete_to_notes()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.DeleteReviewCommentAsync("g", "r", 7, "901", threadId: "disc-1", default);

        var call = _fx.Calls.Single();
        call.Arguments.Should().Contain("DELETE");
        call.Arguments[^1].Should().Be("projects/g%2Fr/merge_requests/7/discussions/disc-1/notes/901");
    }

    [Fact(DisplayName = "DeleteReviewCommentAsync без thread_id бросает 422-kind исключение")]
    public async Task Delete_without_thread_id_throws_422_kind()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(string.Empty));

        var act = async () => await _fx.Provider.DeleteReviewCommentAsync("g", "r", 7, "901", threadId: null, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.ReviewCommentAnchorInvalid);
        _fx.Calls.Should().BeEmpty();
    }

    [Fact(DisplayName = "DeleteReviewCommentAsync мапит 404 в NotFound")]
    public async Task Delete_maps_404_to_not_found()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Fail(1, "HTTP 404: Not Found"));

        var act = async () => await _fx.Provider.DeleteReviewCommentAsync("g", "r", 7, "901", "disc-1", default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NotFound);
    }
}
