using FluentAssertions;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Coverage for
/// <see cref="Throne.Infrastructure.Git.GitHubCli.GitHubCliProvider.ListPullRequestCommentsAsync"/>.
/// The provider walks two GitHub feeds in one call —
/// <c>/issues/{n}/comments</c> (discussion thread) and <c>/pulls/{n}/comments</c>
/// (inline review) — and stores a composite ETag on the binding. Tests pin:
/// the merged-and-sorted output, the per-side <c>If-None-Match</c> routing,
/// the «both 304 → NotModified» short-circuit, the «mixed 304/200 → refetch
/// the 304 side without etag» recovery, and the 404 / rate-limit branches.
/// </summary>
public class GitHubCliProviderPullRequestCommentsTests
{
    private const string IssuesUrl = "/repos/alice/throne/issues/42/comments";
    private const string ReviewUrl = "/repos/alice/throne/pulls/42/comments";

    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "ListPullRequestComments объединяет issues + review и сортирует по created_at")]
    public async Task List_pr_comments_merges_both_feeds_sorted_by_created_at()
    {
        const string issuesJson = """
            [{"id":201,"user":{"login":"manager","avatar_url":"https://gh/m.png"},
              "body":"Could you rebase?","created_at":"2026-05-23T09:00:00Z",
              "html_url":"https://gh/x/y/issues/42#c201"}]
            """;
        const string reviewJson = """
            [{"id":101,"user":{"login":"reviewer","avatar_url":"https://gh/a.png"},
              "body":"Looks good","created_at":"2026-05-23T10:00:00Z",
              "updated_at":"2026-05-23T10:01:00Z",
              "html_url":"https://gh/x/y/pull/42#r101","path":"src/foo.cs"}]
            """;

        _fx.OnRun(req => ReplyFor(req, issuesJson, "\"ie\"", reviewJson, "\"re\""));

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "alice", "throne", 42, etag: null, default);

        var fresh = page.Should().BeOfType<PullRequestCommentsPage.Fresh>().Subject;
        fresh.Comments.Should().HaveCount(2);
        fresh.Comments[0].Id.Should().Be("201");
        fresh.Comments[0].AuthorLogin.Should().Be("manager");
        fresh.Comments[0].Path.Should().BeNull();
        fresh.Comments[1].Id.Should().Be("101");
        fresh.Comments[1].Path.Should().Be("src/foo.cs");

        fresh.Etag.Should().NotBeNull();
        fresh.Etag!.Should().Contain("\"i\":\"\\u0022ie\\u0022\"");
        fresh.Etag.Should().Contain("\"r\":\"\\u0022re\\u0022\"");

        var paths = _fx.Calls.Select(c => c.Arguments[^1]).ToArray();
        paths.Should().Contain(IssuesUrl).And.Contain(ReviewUrl);
        _fx.Calls.Should().OnlyContain(c => !c.Arguments.Any(a => a.StartsWith("If-None-Match", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "ListPullRequestComments рассылает If-None-Match по двум источникам из composite etag")]
    public async Task List_pr_comments_routes_per_side_if_none_match()
    {
        const string composite = """{"i":"\"i-old\"","r":"\"r-old\""}""";
        _fx.OnRun(req => ReplyFor(req, "[]", "\"i-new\"", "[]", "\"r-new\""));

        await _fx.Provider.ListPullRequestCommentsAsync("a", "b", 1, etag: composite, default);

        var issuesCall = _fx.Calls.Single(c => c.Arguments[^1].EndsWith("/issues/1/comments", StringComparison.Ordinal));
        issuesCall.Arguments.Should().ContainInOrder("-H", "If-None-Match: \"i-old\"");

        var reviewCall = _fx.Calls.Single(c => c.Arguments[^1].EndsWith("/pulls/1/comments", StringComparison.Ordinal));
        reviewCall.Arguments.Should().ContainInOrder("-H", "If-None-Match: \"r-old\"");
    }

    [Fact(DisplayName = "ListPullRequestComments возвращает NotModified только когда оба источника 304")]
    public async Task List_pr_comments_returns_not_modified_when_both_sides_are_304()
    {
        _fx.OnRun(req => NotModified());

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "a", "b", 1, etag: """{"i":"\"i\"","r":"\"r\""}""", default);

        page.Should().BeOfType<PullRequestCommentsPage.NotModified>();
        _fx.Calls.Should().HaveCount(2);
    }

    [Fact(DisplayName = "ListPullRequestComments при 304 на одной стороне рефетчит её без etag")]
    public async Task List_pr_comments_refetches_unconditional_when_one_side_is_304()
    {
        const string issuesJson = """
            [{"id":201,"user":{"login":"manager"},"body":"hi",
              "created_at":"2026-05-23T09:00:00Z"}]
            """;
        const string reviewJson = """
            [{"id":101,"user":{"login":"reviewer"},"body":"nit",
              "created_at":"2026-05-23T11:00:00Z","path":"src/foo.cs"}]
            """;

        // Issues side: 200 immediately. Review side: 304 on first call, 200 on refetch (no etag).
        var reviewCallCount = 0;
        _fx.OnRun(req =>
        {
            if (IsIssuesCall(req))
            {
                return Reply("200 OK", "\"i-new\"", issuesJson);
            }

            reviewCallCount++;
            return reviewCallCount == 1
                ? NotModified()
                : Reply("200 OK", "\"r-new\"", reviewJson);
        });

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "a", "b", 1, etag: """{"i":"\"i\"","r":"\"r-old\""}""", default);

        var fresh = page.Should().BeOfType<PullRequestCommentsPage.Fresh>().Subject;
        fresh.Comments.Should().HaveCount(2);
        fresh.Etag!.Should().Contain("\"i-new\"".Replace("\"", "\\u0022"));
        fresh.Etag.Should().Contain("\"r-new\"".Replace("\"", "\\u0022"));

        var reviewCalls = _fx.Calls.Where(IsReviewCall).ToList();
        reviewCalls.Should().HaveCount(2);
        reviewCalls[0].Arguments.Should().Contain("-H");
        reviewCalls[1].Arguments.Should().NotContain("-H");
    }

    [Fact(DisplayName = "ListPullRequestComments(404 на любой стороне) возвращает null")]
    public async Task List_pr_comments_returns_null_on_404_from_any_side()
    {
        _fx.OnRun(req => IsIssuesCall(req)
            ? new ProcessRunResult(1, "HTTP/2.0 404 Not Found\r\n\r\n{\"message\":\"Not Found\"}", "HTTP 404", TimeSpan.Zero)
            : Reply("200 OK", "\"r\"", "[]"));

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "a", "b", 1, etag: null, default);

        page.Should().BeNull();
    }

    [Fact(DisplayName = "ListPullRequestComments пробрасывает rate-limit от любого источника")]
    public async Task List_pr_comments_throws_on_rate_limit_from_any_side()
    {
        const string rateLimited = "HTTP/2.0 403 Forbidden\r\n"
            + "X-RateLimit-Remaining: 0\r\nX-RateLimit-Reset: 1700000000\r\n\r\n"
            + "{\"message\":\"API rate limit exceeded\"}";

        _fx.OnRun(req => IsIssuesCall(req)
            ? new ProcessRunResult(1, rateLimited, "HTTP 403", TimeSpan.Zero)
            : Reply("200 OK", "\"r\"", "[]"));

        var act = async () => await _fx.Provider.ListPullRequestCommentsAsync(
            "a", "b", 1, etag: null, default);

        (await act.Should().ThrowAsync<GitProviderException>())
            .Which.Kind.Should().Be(GitProviderErrorKind.NetworkError);
    }

    private static bool IsIssuesCall(ProcessRunRequest req) =>
        req.Arguments.Count > 0 && req.Arguments[^1].Contains("/issues/", StringComparison.Ordinal);

    private static bool IsReviewCall(ProcessRunRequest req) =>
        req.Arguments.Count > 0 && req.Arguments[^1].Contains("/pulls/", StringComparison.Ordinal)
        && req.Arguments[^1].EndsWith("/comments", StringComparison.Ordinal);

    private static ProcessRunResult ReplyFor(
        ProcessRunRequest req,
        string issuesJson,
        string issuesEtag,
        string reviewJson,
        string reviewEtag) =>
        IsIssuesCall(req)
            ? Reply("200 OK", issuesEtag, issuesJson)
            : Reply("200 OK", reviewEtag, reviewJson);

    private static ProcessRunResult Reply(string status, string etag, string body)
    {
        var raw = $"HTTP/2.0 {status}\r\nETag: {etag}\r\n\r\n{body}";
        return GitHubCliProviderFixture.Ok(raw);
    }

    private static ProcessRunResult NotModified()
    {
        const string raw = "HTTP/2.0 304 Not Modified\r\nETag: \"old\"\r\n\r\n";
        return new ProcessRunResult(ExitCode: 1, StandardOutput: raw, StandardError: "HTTP 304", Elapsed: TimeSpan.Zero);
    }
}
