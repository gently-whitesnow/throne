using FluentAssertions;
using Throne.Application.Git;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GitHubCliProviderMergeTests
{
    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "GetPullRequestMergeStatusAsync читает gh pr view --json и мапит mergeable/checks")]
    public async Task GetMergeStatus_maps_mergeable_and_checks()
    {
        const string body = """
            {"mergeable":"MERGEABLE","mergeStateStatus":"CLEAN",
             "statusCheckRollup":[{"__typename":"CheckRun","status":"COMPLETED","conclusion":"SUCCESS"}],
             "url":"https://github.com/o/r/pull/42"}
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(body));

        var status = await _fx.Provider.GetPullRequestMergeStatusAsync("o", "r", 42, default);

        status.Should().NotBeNull();
        status!.Mergeability.Should().Be(PullRequestMergeability.Mergeable);
        status.Checks.Should().Be(PullRequestChecksState.Passing);
        status.HtmlUrl.Should().Be("https://github.com/o/r/pull/42");
        _fx.Calls.Single().Arguments.Should().Contain("--json");
    }

    [Fact(DisplayName = "GetPullRequestMergeStatusAsync: красный чек → failing, UNKNOWN mergeable → checking")]
    public async Task GetMergeStatus_failing_check_and_unknown_mergeability()
    {
        const string body = """
            {"mergeable":"UNKNOWN","mergeStateStatus":"UNKNOWN",
             "statusCheckRollup":[{"__typename":"StatusContext","state":"FAILURE"}]}
            """;
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(body));

        var status = await _fx.Provider.GetPullRequestMergeStatusAsync("o", "r", 42, default);

        status!.Mergeability.Should().Be(PullRequestMergeability.Checking);
        status.Checks.Should().Be(PullRequestChecksState.Failing);
    }

    [Fact(DisplayName = "MergePullRequestAsync шлёт gh pr merge со стратегией и --delete-branch")]
    public async Task Merge_sends_strategy_and_delete_branch()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok("✓ Squashed and merged pull request #42"));

        var result = await _fx.Provider.MergePullRequestAsync(
            "o", "r", 42, new MergePullRequestRequest(MergeStrategy.Squash, DeleteBranch: true), default);

        result.Merged.Should().BeTrue();
        var args = _fx.Calls.Single().Arguments;
        args.Should().ContainInOrder("pr", "merge", "42");
        args.Should().Contain("--squash");
        args.Should().Contain("--delete-branch");
    }

    [Fact(DisplayName = "MergePullRequestAsync: отказ провайдера → MergeNotAllowed с деталью")]
    public async Task Merge_refusal_maps_to_merge_not_allowed()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Fail(
            1, "Pull request #42 is not mergeable: the base branch policy prohibits the merge."));

        var act = async () => await _fx.Provider.MergePullRequestAsync(
            "o", "r", 42, new MergePullRequestRequest(MergeStrategy.Merge, DeleteBranch: false), default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.MergeNotAllowed);
        ex.Detail.Should().Contain("not mergeable");
    }
}
