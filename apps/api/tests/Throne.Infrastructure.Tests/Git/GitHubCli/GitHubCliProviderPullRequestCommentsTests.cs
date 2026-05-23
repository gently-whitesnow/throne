using FluentAssertions;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Coverage for
/// <see cref="Throne.Infrastructure.Git.GitHubCli.GitHubCliProvider.ListPullRequestCommentsAsync"/>.
/// Exercises ADR-0024 § 4 conditional-GET semantics: 200 returns a
/// <see cref="PullRequestCommentsPage.Fresh"/> with the parsed ETag, 304
/// returns <see cref="PullRequestCommentsPage.NotModified"/>, 404 → null. The
/// <c>If-None-Match</c> header is only sent when an ETag is provided. Per D3
/// of the slice review the method tracks ONLY the review-comments feed
/// (<c>/pulls/{n}/comments</c>) — issue-comments are out of scope for slice 1.
/// </summary>
public class GitHubCliProviderPullRequestCommentsTests
{
    private const string CommentsBody = """
        [{"id":101,"user":{"login":"reviewer","avatar_url":"https://gh/a.png"},
          "body":"Looks good","created_at":"2026-05-23T10:00:00Z",
          "updated_at":"2026-05-23T10:01:00Z",
          "html_url":"https://gh/x/y/pull/42#r101","path":"src/foo.cs"}]
        """;

    private static readonly string[] CommentsArgs =
        ["api", "-i", "/repos/alice/throne/pulls/42/comments"];

    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "ListPullRequestComments(200) парсит ленту и ETag")]
    public async Task List_pr_comments_parses_fresh_page_with_etag()
    {
        var raw = "HTTP/2.0 200 OK\r\nETag: W/\"xyz\"\r\n\r\n" + CommentsBody;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(raw));

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "alice", "throne", 42, etag: null, default);

        var fresh = page.Should().BeOfType<PullRequestCommentsPage.Fresh>().Subject;
        fresh.Etag.Should().Be("W/\"xyz\"");
        fresh.Comments.Should().ContainSingle();
        var comment = fresh.Comments.Single();
        comment.Id.Should().Be("101");
        comment.AuthorLogin.Should().Be("reviewer");
        comment.Path.Should().Be("src/foo.cs");
        comment.UpdatedAt.Should().NotBeNull();

        var call = _fx.Calls.Single();
        call.Arguments.Should().BeEquivalentTo(CommentsArgs, options => options.WithStrictOrdering());
        call.Arguments.Should().NotContain(a => a.StartsWith("If-None-Match", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "ListPullRequestComments присылает If-None-Match при наличии etag")]
    public async Task List_pr_comments_sends_if_none_match_header()
    {
        const string raw = "HTTP/2.0 200 OK\r\nETag: \"new\"\r\n\r\n[]";
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(raw));

        await _fx.Provider.ListPullRequestCommentsAsync("a", "b", 1, etag: "\"old\"", default);

        var args = _fx.Calls.Single().Arguments;
        args.Should().ContainInOrder("-H", "If-None-Match: \"old\"");
    }

    [Fact(DisplayName = "ListPullRequestComments(304) возвращает NotModified")]
    public async Task List_pr_comments_returns_not_modified_on_304()
    {
        // gh api on 304 typically still exits non-zero, but the status line on
        // stdout drives the decision — exit code is intentionally non-zero here.
        const string raw = "HTTP/2.0 304 Not Modified\r\nETag: \"old\"\r\n\r\n";
        _fx.OnRun(_ => new ProcessRunResult(
            ExitCode: 1, StandardOutput: raw, StandardError: "HTTP 304", Elapsed: TimeSpan.Zero));

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "a", "b", 1, etag: "\"old\"", default);

        page.Should().BeOfType<PullRequestCommentsPage.NotModified>();
    }

    [Fact(DisplayName = "ListPullRequestComments(404) возвращает null")]
    public async Task List_pr_comments_returns_null_on_404()
    {
        const string raw = "HTTP/2.0 404 Not Found\r\n\r\n{\"message\":\"Not Found\"}";
        _fx.OnRun(_ => new ProcessRunResult(
            ExitCode: 1, StandardOutput: raw, StandardError: "HTTP 404: Not Found", Elapsed: TimeSpan.Zero));

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "a", "b", 1, etag: null, default);

        page.Should().BeNull();
    }

    [Fact(DisplayName = "ListPullRequestComments при X-RateLimit-Remaining:0 бросает NetworkError")]
    public async Task List_pr_comments_throws_on_rate_limit()
    {
        const string raw = "HTTP/2.0 403 Forbidden\r\n"
            + "X-RateLimit-Remaining: 0\r\nX-RateLimit-Reset: 1700000000\r\n\r\n"
            + "{\"message\":\"API rate limit exceeded\"}";
        _fx.OnRun(_ => new ProcessRunResult(
            ExitCode: 1, StandardOutput: raw, StandardError: "HTTP 403", Elapsed: TimeSpan.Zero));

        var act = async () => await _fx.Provider.ListPullRequestCommentsAsync(
            "a", "b", 1, etag: null, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NetworkError);
        ex.Detail.Should().Contain("X-RateLimit-Reset");
    }
}
