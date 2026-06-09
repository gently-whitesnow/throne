using FluentAssertions;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Slice B coverage: graphql thread enrichment joined onto the review feed, the
/// resolve/unresolve mutation, and the delete-by-id path.
/// </summary>
public class GitHubCliProviderReviewThreadTests
{
    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "ListPullRequestComments обогащает review-комментарии resolved/thread_id из graphql")]
    public async Task List_comments_enriches_review_side_from_graphql()
    {
        const string reviewJson = """
            [{"id":101,"user":{"login":"reviewer"},"body":"nit","created_at":"2026-05-23T10:00:00Z",
              "path":"src/Foo.cs","line":12,"side":"RIGHT"}]
            """;
        const string issuesJson = """
            [{"id":201,"user":{"login":"manager"},"body":"hi","created_at":"2026-05-23T09:00:00Z"}]
            """;
        const string graphqlJson = """
            {"data":{"repository":{"pullRequest":{"reviewThreads":{"nodes":[
              {"id":"THREAD_1","isResolved":true,"comments":{"nodes":[{"databaseId":101}]}}]}}}}}
            """;
        _fx.OnRun(req =>
        {
            if (IsGraphql(req))
            {
                return GitHubCliProviderFixture.Ok(graphqlJson);
            }
            return IsIssuesCall(req)
                ? Reply(issuesJson, "\"i\"")
                : Reply(reviewJson, "\"r\"");
        });

        var page = await _fx.Provider.ListPullRequestCommentsAsync("o", "r", 42, etag: null, default);

        var fresh = page.Should().BeOfType<PullRequestCommentsPage.Fresh>().Subject;
        var review = fresh.Comments.Single(c => c.Id == "101");
        review.Resolved.Should().BeTrue();
        review.ThreadId.Should().Be("THREAD_1");
        // The issue comment is never part of a resolvable thread.
        var issue = fresh.Comments.Single(c => c.Id == "201");
        issue.Resolved.Should().BeNull();
        issue.ThreadId.Should().BeNull();
    }

    [Fact(DisplayName = "ResolveReviewThreadAsync(resolved=true) шлёт resolveReviewThread и парсит isResolved")]
    public async Task Resolve_builds_resolve_mutation()
    {
        const string body = """
            {"data":{"resolveReviewThread":{"thread":{"id":"T1","isResolved":true}}}}
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(body));

        var state = await _fx.Provider.ResolveReviewThreadAsync("o", "r", 42, "T1", resolved: true, default);

        state.ThreadId.Should().Be("T1");
        state.Resolved.Should().BeTrue();
        var call = _fx.Calls.Single();
        call.Arguments.Should().Contain("graphql");
        call.Arguments.Should().Contain(a => a.Contains("resolveReviewThread", StringComparison.Ordinal));
        call.Arguments.Should().Contain("threadId=T1");
    }

    [Fact(DisplayName = "ResolveReviewThreadAsync(resolved=false) шлёт unresolveReviewThread")]
    public async Task Unresolve_builds_unresolve_mutation()
    {
        const string body = """
            {"data":{"unresolveReviewThread":{"thread":{"id":"T1","isResolved":false}}}}
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(body));

        var state = await _fx.Provider.ResolveReviewThreadAsync("o", "r", 42, "T1", resolved: false, default);

        state.Resolved.Should().BeFalse();
        _fx.Calls.Single().Arguments.Should().Contain(a => a.Contains("unresolveReviewThread", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ResolveReviewThreadAsync мапит graphql errors с NOT_FOUND в NotFound")]
    public async Task Resolve_maps_graphql_not_found()
    {
        const string body = """
            {"data":null,"errors":[{"type":"NOT_FOUND","message":"Could not resolve to a node"}]}
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(body));

        var act = async () => await _fx.Provider.ResolveReviewThreadAsync("o", "r", 42, "missing", true, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NotFound);
    }

    [Fact(DisplayName = "DeleteReviewCommentAsync шлёт DELETE на pulls/comments/{id}")]
    public async Task Delete_issues_delete_to_pulls_comments()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok("HTTP/2.0 204 No Content\r\n\r\n"));

        await _fx.Provider.DeleteReviewCommentAsync("o", "r", 42, "987", threadId: null, default);

        var call = _fx.Calls.Single();
        call.Arguments.Should().Contain("DELETE");
        call.Arguments[^1].Should().Be("/repos/o/r/pulls/comments/987");
    }

    [Fact(DisplayName = "DeleteReviewCommentAsync мапит 404 в GitProviderException NotFound")]
    public async Task Delete_maps_404_to_not_found()
    {
        _fx.OnRun(_ => new ProcessRunResult(
            ExitCode: 1,
            StandardOutput: "HTTP/2.0 404 Not Found\r\n\r\n{\"message\":\"Not Found\"}",
            StandardError: "HTTP 404",
            Elapsed: TimeSpan.Zero));

        var act = async () => await _fx.Provider.DeleteReviewCommentAsync("o", "r", 42, "987", null, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NotFound);
    }

    private static bool IsGraphql(ProcessRunRequest req) =>
        req.Arguments.Count > 1 && req.Arguments[1] == "graphql";

    private static bool IsIssuesCall(ProcessRunRequest req) =>
        req.Arguments[^1].Contains("/issues/", StringComparison.Ordinal);

    private static ProcessRunResult Reply(string body, string etag) =>
        GitHubCliProviderFixture.Ok($"HTTP/2.0 200 OK\r\nETag: {etag}\r\n\r\n{body}");
}
