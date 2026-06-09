using FluentAssertions;
using Throne.Application.Git;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GitLabCliProviderMergeTests
{
    private readonly GitLabCliProviderFixture _fx = new();

    [Fact(DisplayName = "GetPullRequestMergeStatusAsync читает MR JSON и мапит detailed_merge_status/pipeline")]
    public async Task GetMergeStatus_maps_detailed_status_and_pipeline()
    {
        const string body = """
            {"iid":7,"detailed_merge_status":"mergeable",
             "head_pipeline":{"status":"running"},
             "web_url":"https://gitlab.example.com/o/r/-/merge_requests/7"}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(body));

        var status = await _fx.Provider.GetPullRequestMergeStatusAsync("o", "r", 7, default);

        status.Should().NotBeNull();
        status!.Mergeability.Should().Be(PullRequestMergeability.Mergeable);
        status.Checks.Should().Be(PullRequestChecksState.Pending);
        status.HtmlUrl.Should().Be("https://gitlab.example.com/o/r/-/merge_requests/7");
        GitLabCliProviderFixture.HasGitLabHost(_fx.Calls.Single()).Should().BeTrue();
    }

    [Fact(DisplayName = "GetPullRequestMergeStatusAsync: need_rebase → behind, failed pipeline → failing")]
    public async Task GetMergeStatus_behind_and_failing()
    {
        const string body = """
            {"detailed_merge_status":"need_rebase","head_pipeline":{"status":"failed"}}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(body));

        var status = await _fx.Provider.GetPullRequestMergeStatusAsync("o", "r", 7, default);

        status!.Mergeability.Should().Be(PullRequestMergeability.Behind);
        status.Checks.Should().Be(PullRequestChecksState.Failing);
    }

    [Fact(DisplayName = "MergePullRequestAsync шлёт glab mr merge со стратегией и --remove-source-branch")]
    public async Task Merge_sends_strategy_and_remove_source_branch()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok("✓ Merged merge request !7"));

        var result = await _fx.Provider.MergePullRequestAsync(
            "o", "r", 7, new MergePullRequestRequest(MergeStrategy.Rebase, DeleteBranch: true), default);

        result.Merged.Should().BeTrue();
        var args = _fx.Calls.Single().Arguments;
        args.Should().ContainInOrder("mr", "merge", "7");
        args.Should().Contain("-R");
        args.Should().Contain("o/r");
        args.Should().Contain("--yes");
        args.Should().Contain("--rebase");
        args.Should().Contain("--remove-source-branch");
    }

    [Fact(DisplayName = "MergePullRequestAsync: отказ провайдера → MergeNotAllowed с деталью")]
    public async Task Merge_refusal_maps_to_merge_not_allowed()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Fail(
            1, "POST .../merge: 405 Method Not Allowed (the merge request is not mergeable)"));

        var act = async () => await _fx.Provider.MergePullRequestAsync(
            "o", "r", 7, new MergePullRequestRequest(MergeStrategy.Merge, DeleteBranch: false), default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.MergeNotAllowed);
    }
}
