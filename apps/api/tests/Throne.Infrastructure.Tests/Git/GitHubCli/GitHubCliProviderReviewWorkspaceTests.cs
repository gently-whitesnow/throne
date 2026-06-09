using FluentAssertions;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GitHubCliProviderReviewWorkspaceTests
{
    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "GetPullRequestDiffAsync читает PR JSON и /files и комбинирует SHAs")]
    public async Task GetPullRequestDiff_combines_shas_and_files()
    {
        const string prBody = """
            {"number":42,"state":"open","base":{"sha":"basesha"},"head":{"sha":"headsha"}}
            """;
        const string filesBody = """
            [{"filename":"a.cs","status":"modified","patch":"@@ -1 +1 @@\n-a\n+b"}]
            """;
        _fx.OnRun(req =>
        {
            var path = req.Arguments[req.Arguments.Count - 1];
            // /files runs paginated (no -i): raw body. /pulls/{n} keeps -i headers.
            return path.Contains("/files")
                ? GitHubCliProviderFixture.Ok(filesBody)
                : GitHubCliProviderFixture.Ok($"HTTP/1.1 200 OK\r\n\r\n{prBody}");
        });

        var diff = await _fx.Provider.GetPullRequestDiffAsync("o", "r", 42, default);

        diff.Should().NotBeNull();
        diff!.BaseSha.Should().Be("basesha");
        diff.HeadSha.Should().Be("headsha");
        diff.StartSha.Should().Be("basesha");
        diff.Files.Should().ContainSingle().Which.Path.Should().Be("a.cs");
        _fx.Calls.Should().HaveCount(2);
        _fx.Calls.Single(c => c.Arguments[c.Arguments.Count - 1].Contains("/files"))
            .Arguments.Should().Contain("--paginate");
    }

    [Fact(DisplayName = "GetCommitDiffAsync читает один commit JSON и парсит parent")]
    public async Task GetCommitDiff_reads_single_commit_payload()
    {
        const string body = """
            {"sha":"head","parents":[{"sha":"parent"}],
             "files":[{"filename":"a.cs","status":"added","patch":"+x"}]}
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok($"HTTP/1.1 200 OK\r\n\r\n{body}"));

        var diff = await _fx.Provider.GetCommitDiffAsync("o", "r", "head", default);

        diff!.BaseSha.Should().Be("parent");
        diff.HeadSha.Should().Be("head");
        diff.Files.Should().ContainSingle();
    }

    [Fact(DisplayName = "ListPullRequestCommitsAsync читает /pulls/{n}/commits")]
    public async Task ListPullRequestCommitsAsync_reads_commits()
    {
        const string body = """
            [{"sha":"aaa","commit":{"message":"feat: x","author":{"name":"a","date":"2026-05-23T10:00:00Z"}},
              "author":{"login":"alice"}}]
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(body));

        var commits = await _fx.Provider.ListPullRequestCommitsAsync("o", "r", 42, default);

        commits.Should().NotBeNull();
        commits!.Should().ContainSingle().Which.AuthorLogin.Should().Be("alice");
        var call = _fx.Calls.Single();
        call.Arguments.Should().Contain("--paginate");
        call.Arguments[2].Should().StartWith("/repos/o/r/pulls/42/commits");
    }

    [Fact(DisplayName = "GetPullRequestDiffAsync склеивает многостраничный --paginate ответ /files")]
    public async Task GetPullRequestDiff_reads_multipage_files()
    {
        const string prBody = """
            {"number":42,"state":"open","base":{"sha":"basesha"},"head":{"sha":"headsha"}}
            """;
        // gh --paginate concatenates pages as [..][..] with no delimiter.
        var page1 = BuildFilesArray(start: 0, count: 100);
        var page2 = BuildFilesArray(start: 100, count: 50);
        _fx.OnRun(req =>
        {
            var path = req.Arguments[req.Arguments.Count - 1];
            return path.Contains("/files")
                ? GitHubCliProviderFixture.Ok(page1 + page2)
                : GitHubCliProviderFixture.Ok($"HTTP/1.1 200 OK\r\n\r\n{prBody}");
        });

        var diff = await _fx.Provider.GetPullRequestDiffAsync("o", "r", 42, default);

        diff!.Files.Should().HaveCount(150);
        diff.Files[0].Path.Should().Be("f0.cs");
        diff.Files[^1].Path.Should().Be("f149.cs");
    }

    [Fact(DisplayName = "GetPullRequestDiffAsync: rate-limit на /files → NetworkError")]
    public async Task GetPullRequestDiff_rate_limited_files_maps_to_network_error()
    {
        const string prBody = """
            {"number":42,"state":"open","base":{"sha":"basesha"},"head":{"sha":"headsha"}}
            """;
        _fx.OnRun(req =>
        {
            var path = req.Arguments[req.Arguments.Count - 1];
            return path.Contains("/files")
                ? GitHubCliProviderFixture.Fail(1, "gh: API rate limit exceeded for user ID 1 (HTTP 403)")
                : GitHubCliProviderFixture.Ok($"HTTP/1.1 200 OK\r\n\r\n{prBody}");
        });

        var act = async () => await _fx.Provider.GetPullRequestDiffAsync("o", "r", 42, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NetworkError);
    }

    [Fact(DisplayName = "GetPullRequestDiffAsync: 404 на /files → null")]
    public async Task GetPullRequestDiff_not_found_files_returns_null()
    {
        const string prBody = """
            {"number":42,"state":"open","base":{"sha":"basesha"},"head":{"sha":"headsha"}}
            """;
        _fx.OnRun(req =>
        {
            var path = req.Arguments[req.Arguments.Count - 1];
            return path.Contains("/files")
                ? GitHubCliProviderFixture.Fail(1, "gh: Not Found (HTTP 404)")
                : GitHubCliProviderFixture.Ok($"HTTP/1.1 200 OK\r\n\r\n{prBody}");
        });

        var diff = await _fx.Provider.GetPullRequestDiffAsync("o", "r", 42, default);

        diff.Should().BeNull();
    }

    private static string BuildFilesArray(int start, int count)
    {
        var items = Enumerable.Range(start, count)
            .Select(i => $$"""{"filename":"f{{i}}.cs","status":"modified","patch":"@@ -1 +1 @@\n-a\n+b"}""");
        return "[" + string.Join(",", items) + "]";
    }

    [Fact(DisplayName = "SubmitReviewCommentAsync шлёт POST с anchor-полями")]
    public async Task SubmitReviewCommentAsync_posts_anchor_fields()
    {
        const string responseBody = """
            {"id":987,"user":{"login":"alice"},"body":"hello",
             "created_at":"2026-05-23T12:00:00Z","html_url":"https://gh/x/y/pull/42#discussion_r987"}
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok($"HTTP/1.1 201 Created\r\n\r\n{responseBody}"));

        var request = new SubmitReviewCommentRequest(
            Body: "hello",
            Path: "src/a.cs",
            PreviousPath: null,
            Side: ReviewCommentSide.Right,
            Line: 12,
            CommitSha: "headsha",
            BaseSha: "basesha",
            StartSha: "basesha");
        var result = await _fx.Provider.SubmitReviewCommentAsync("o", "r", 42, request, default);

        result.Id.Should().Be("987");
        result.AuthorLogin.Should().Be("alice");
        var call = _fx.Calls.Single();
        call.Arguments.Should().Contain("POST");
        call.Arguments.Should().Contain(a => a == "body=hello");
        call.Arguments.Should().Contain(a => a == "commit_id=headsha");
        call.Arguments.Should().Contain(a => a == "side=RIGHT");
        call.Arguments.Should().Contain(a => a == "line=12");
    }

    [Fact(DisplayName = "SubmitReviewCommentAsync мапит 422 в ReviewCommentAnchorInvalid")]
    public async Task SubmitReviewCommentAsync_maps_422_to_anchor_invalid()
    {
        const string body = """{"message":"pull_request_review_thread: position invalid"}""";
        _fx.OnRun(_ => new ProcessRunResult(
            ExitCode: 0,
            StandardOutput: $"HTTP/1.1 422 Unprocessable Entity\r\n\r\n{body}",
            StandardError: string.Empty,
            Elapsed: TimeSpan.Zero));

        var request = new SubmitReviewCommentRequest("x", "a.cs", null, ReviewCommentSide.Right, 1, "h", "b", "b");

        var act = async () => await _fx.Provider.SubmitReviewCommentAsync("o", "r", 42, request, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.ReviewCommentAnchorInvalid);
    }
}
