using FluentAssertions;
using Throne.Application.Git;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GitLabCliProviderPullRequestCommentsTests
{
    private readonly GitLabCliProviderFixture _fx = new();

    [Fact(DisplayName = "ListPullRequestCommentsAsync парсит discussions, фильтрует system notes и отдаёт etag=null")]
    public async Task List_pull_request_comments_parses_discussions()
    {
        const string json = """
            [{"id":"d1","notes":[
              {"id":101,"system":true,"author":{"username":"bot"},"body":"changed title","created_at":"2026-06-07T10:00:00Z"},
              {"id":102,"system":false,"author":{"username":"reviewer","avatar_url":"https://gitlab.example.com/a.png"},
               "body":"Please adjust","created_at":"2026-06-07T10:01:00Z","updated_at":"2026-06-07T10:02:00Z",
               "html_url":"https://gitlab.example.com/g/r/-/merge_requests/42#note_102",
               "position":{"new_path":"src/Foo.cs"}}]}]
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(json));

        var page = await _fx.Provider.ListPullRequestCommentsAsync(
            "g", "r", 42, etag: "\"ignored\"", ct: default);

        var fresh = page.Should().BeOfType<PullRequestCommentsPage.Fresh>().Subject;
        fresh.Etag.Should().BeNull();
        var comment = fresh.Comments.Should().ContainSingle().Subject;
        comment.Id.Should().Be("102");
        comment.AuthorLogin.Should().Be("reviewer");
        comment.Path.Should().Be("src/Foo.cs");
        comment.UpdatedAt.Should().NotBeNull();
        _fx.Calls.Single().Arguments.Should().BeEquivalentTo(
            ["api", "projects/g%2Fr/merge_requests/42/discussions", "--paginate"],
            o => o.WithStrictOrdering());
    }

    [Fact(DisplayName = "ListPullRequestCommentsAsync склеивает страницы --paginate и не ломается на подстроке ][ в теле")]
    public async Task List_pull_request_comments_merges_paginated_pages_with_bracket_substring()
    {
        // glab api --paginate конкатенирует страницы как [..][..] без разделителя.
        // Тело первого комментария содержит "][": наивный replace "]["→"," порвал бы JSON.
        const string json = """
            [{"id":"d1","notes":[
              {"id":101,"system":false,"author":{"username":"reviewer"},
               "body":"array slice a[1][2] and json ][ here","created_at":"2026-06-07T10:01:00Z"}]}][{"id":"d2","notes":[
              {"id":102,"system":false,"author":{"username":"maintainer"},
               "body":"second page","created_at":"2026-06-07T10:03:00Z"}]}]
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(json));

        var page = await _fx.Provider.ListPullRequestCommentsAsync("g", "r", 42, null, default);

        var fresh = page.Should().BeOfType<PullRequestCommentsPage.Fresh>().Subject;
        fresh.Comments.Should().HaveCount(2);
        fresh.Comments[0].Body.Should().Be("array slice a[1][2] and json ][ here");
        fresh.Comments[1].Id.Should().Be("102");
        fresh.Comments[1].Body.Should().Be("second page");
    }

    [Fact(DisplayName = "ListPullRequestCommentsAsync возвращает null на 404")]
    public async Task List_pull_request_comments_returns_null_on_404()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Fail(1, "HTTP 404: Not Found"));

        var page = await _fx.Provider.ListPullRequestCommentsAsync("g", "r", 42, null, default);

        page.Should().BeNull();
    }
}
