using System.Text.Json;
using FluentAssertions;
using Throne.Application.Git;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GitLabCliProviderReviewWorkspaceTests
{
    private readonly GitLabCliProviderFixture _fx = new();

    [Fact(DisplayName = "GetPullRequestDiffAsync читает MR JSON и /diffs")]
    public async Task GetPullRequestDiff_combines_refs_and_files()
    {
        const string mrBody = """
            {"iid":7,"diff_refs":{"base_sha":"b","head_sha":"h","start_sha":"s"}}
            """;
        const string diffsBody = """
            [{"new_path":"a.cs","old_path":"a.cs","diff":"@@ -1 +1 @@\n-a\n+b","new_file":false}]
            """;
        _fx.OnRun(req =>
        {
            var apiPath = req.Arguments[1];
            return apiPath.EndsWith("/diffs", StringComparison.Ordinal)
                ? GitLabCliProviderFixture.Ok(diffsBody)
                : GitLabCliProviderFixture.Ok(mrBody);
        });

        var diff = await _fx.Provider.GetPullRequestDiffAsync("g", "r", 7, default);

        diff.Should().NotBeNull();
        diff!.BaseSha.Should().Be("b");
        diff.HeadSha.Should().Be("h");
        diff.StartSha.Should().Be("s");
        diff.Files.Should().ContainSingle().Which.Path.Should().Be("a.cs");
        _fx.Calls.Should().HaveCount(2);
        _fx.Calls.All(GitLabCliProviderFixture.HasGitLabHost).Should().BeTrue();
    }

    [Fact(DisplayName = "GetCommitDiffAsync читает commit и diff и поднимает parent_ids")]
    public async Task GetCommitDiff_reads_commit_and_diff()
    {
        const string commitBody = """{"id":"head","parent_ids":["parent"]}""";
        const string diffBody = """
            [{"new_path":"a.cs","old_path":"a.cs","diff":"+x","new_file":true}]
            """;
        _fx.OnRun(req =>
        {
            var apiPath = req.Arguments[1];
            return apiPath.EndsWith("/diff", StringComparison.Ordinal)
                ? GitLabCliProviderFixture.Ok(diffBody)
                : GitLabCliProviderFixture.Ok(commitBody);
        });

        var diff = await _fx.Provider.GetCommitDiffAsync("g", "r", "head", default);

        diff!.BaseSha.Should().Be("parent");
        diff.HeadSha.Should().Be("head");
        diff.Files.Should().ContainSingle();
    }

    [Fact(DisplayName = "ListPullRequestCommitsAsync проксирует /merge_requests/{iid}/commits --paginate")]
    public async Task ListCommits_uses_paginate()
    {
        const string body = """
            [{"id":"aaa","message":"feat: x","author_name":"Alice","committed_date":"2026-05-23T10:00:00Z"}]
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(body));

        var commits = await _fx.Provider.ListPullRequestCommitsAsync("g", "r", 7, default);

        commits.Should().NotBeNull();
        commits!.Should().ContainSingle();
        _fx.Calls.Single().Arguments.Should().Contain("--paginate");
    }

    [Fact(DisplayName = "SubmitReviewCommentAsync шлёт JSON-тело с вложенным position через stdin")]
    public async Task SubmitReviewCommentAsync_posts_position_fields()
    {
        const string responseBody = """
            {"id":"d1","notes":[
              {"id":901,"author":{"username":"alice"},"body":"hello",
               "created_at":"2026-05-23T12:00:00Z","html_url":"https://gl/g/r/-/merge_requests/7#note_901",
               "system":false}]}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(responseBody));

        var request = new SubmitReviewCommentRequest(
            Body: "hello",
            Path: "src/a.cs",
            PreviousPath: null,
            Side: ReviewCommentSide.Right,
            OldLine: null,
            NewLine: 12,
            CommitSha: "h",
            BaseSha: "b",
            StartSha: "s");

        var result = await _fx.Provider.SubmitReviewCommentAsync("g", "r", 7, request, default);

        result.Id.Should().Be("901");
        result.AuthorLogin.Should().Be("alice");
        var call = _fx.Calls.Single();
        call.Arguments.Should().Contain("POST");
        call.Arguments.Should().Contain("Content-Type: application/json");
        call.Arguments.Should().ContainInOrder("--input", "-");
        call.Arguments.Should().NotContain(a => a.StartsWith("position[", StringComparison.Ordinal));

        using var payload = JsonDocument.Parse(call.StandardInput!);
        var root = payload.RootElement;
        root.GetProperty("body").GetString().Should().Be("hello");
        var position = root.GetProperty("position");
        position.GetProperty("position_type").GetString().Should().Be("text");
        position.GetProperty("base_sha").GetString().Should().Be("b");
        position.GetProperty("head_sha").GetString().Should().Be("h");
        position.GetProperty("start_sha").GetString().Should().Be("s");
        position.GetProperty("new_path").GetString().Should().Be("src/a.cs");
        position.GetProperty("old_path").GetString().Should().Be("src/a.cs");
        position.GetProperty("new_line").GetInt32().Should().Be(12);
        position.TryGetProperty("old_line", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "SubmitReviewCommentAsync на context-строке шлёт оба new_line и old_line")]
    public async Task SubmitReviewCommentAsync_context_row_sends_both_lines()
    {
        const string responseBody = """
            {"id":"d2","notes":[
              {"id":902,"author":{"username":"alice"},"body":"hi",
               "created_at":"2026-05-23T12:00:00Z","html_url":"https://gl/g/r/-/merge_requests/7#note_902",
               "system":false}]}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(responseBody));

        var request = new SubmitReviewCommentRequest(
            Body: "hi",
            Path: "src/a.cs",
            PreviousPath: null,
            Side: ReviewCommentSide.Right,
            OldLine: 11,
            NewLine: 12,
            CommitSha: "h",
            BaseSha: "b",
            StartSha: "s");

        await _fx.Provider.SubmitReviewCommentAsync("g", "r", 7, request, default);

        using var payload = JsonDocument.Parse(_fx.Calls.Single().StandardInput!);
        var position = payload.RootElement.GetProperty("position");
        position.GetProperty("new_line").GetInt32().Should().Be(12);
        position.GetProperty("old_line").GetInt32().Should().Be(11);
    }

    [Fact(DisplayName = "SubmitReviewCommentAsync на del-строке шлёт только old_line")]
    public async Task SubmitReviewCommentAsync_del_row_sends_only_old_line()
    {
        const string responseBody = """
            {"id":"d3","notes":[
              {"id":903,"author":{"username":"alice"},"body":"x",
               "created_at":"2026-05-23T12:00:00Z","system":false}]}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(responseBody));

        var request = new SubmitReviewCommentRequest(
            Body: "x",
            Path: "src/a.cs",
            PreviousPath: null,
            Side: ReviewCommentSide.Left,
            OldLine: 5,
            NewLine: null,
            CommitSha: "h",
            BaseSha: "b",
            StartSha: "s");

        await _fx.Provider.SubmitReviewCommentAsync("g", "r", 7, request, default);

        using var payload = JsonDocument.Parse(_fx.Calls.Single().StandardInput!);
        var position = payload.RootElement.GetProperty("position");
        position.GetProperty("old_line").GetInt32().Should().Be(5);
        position.TryGetProperty("new_line", out _).Should().BeFalse();
    }

    [Fact(DisplayName = "SubmitReviewCommentAsync мапит line_code-ошибку в ReviewCommentAnchorInvalid")]
    public async Task SubmitReviewCommentAsync_maps_line_code_error_to_anchor_invalid()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Fail(
            1, "glab: 400 Bad request - Note {:line_code=>[\"must be a valid line code\"]}"));

        var act = async () => await _fx.Provider.SubmitReviewCommentAsync("g", "r", 7, Anchor(), default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.ReviewCommentAnchorInvalid);
    }

    [Fact(DisplayName = "SubmitReviewCommentAsync НЕ выдаёт anchor_invalid на body-level 400 и сохраняет тело")]
    public async Task SubmitReviewCommentAsync_generic_400_is_not_anchor_invalid()
    {
        // Body-level failure (e.g. a corrupted JSON body) must surface as-is, not be
        // mislabelled as an invalid anchor that masks the real cause.
        _fx.OnRun(_ => GitLabCliProviderFixture.Fail(1, "{\"error\":\"Invalid JSON format\"}\nglab: HTTP 400"));

        var act = async () => await _fx.Provider.SubmitReviewCommentAsync("g", "r", 7, Anchor(), default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().NotBe(GitProviderErrorKind.ReviewCommentAnchorInvalid);
        ex.Detail.Should().Contain("Invalid JSON format");
    }

    private static SubmitReviewCommentRequest Anchor() =>
        new(
            Body: "x",
            Path: "src/a.cs",
            PreviousPath: null,
            Side: ReviewCommentSide.Right,
            OldLine: null,
            NewLine: 12,
            CommitSha: "h",
            BaseSha: "b",
            StartSha: "s");
}
