using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

public class IntentRepositoryBindingFactoryTests
{
    private static readonly DateTimeOffset Now = IntentRepositoryBindingTestBuilder.Now;

    [Fact(DisplayName = "Create стартует в pending без ошибки клона и без PR")]
    public void Create_starts_pending()
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending();

        binding.State.CloneStatus.Should().Be(CloneStatusNames.Pending);
        binding.State.CloneError.Should().BeNull();
        binding.State.PullRequestNumber.Should().BeNull();
        binding.State.PullRequestState.Should().BeNull();
        binding.State.ReviewCommentsEtag.Should().BeNull();
        binding.State.LastSyncedAt.Should().BeNull();
        binding.CreatedAt.Should().Be(Now);
        binding.State.UpdatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "Create с pull_request_number цепляет PR, но state остаётся null")]
    public void Create_with_pr_keeps_state_null()
    {
        var binding = IntentRepositoryBindingTestBuilder.Pending(prNumber: 42);

        binding.State.PullRequestNumber.Should().Be(42);
        binding.State.PullRequestState.Should().BeNull();
    }

    [Fact(DisplayName = "Create отвергает pull_request_number < 1")]
    public void Create_rejects_invalid_pr_number()
    {
        var act = () => IntentRepositoryBindingFactory.Create(
            id: BindingId.New(),
            intentId: new IntentId("i"),
            coordinate: new RepoCoordinate(GitProviderNames.GitHub, "o", "r"),
            defaultBranch: "main",
            workspacePath: "/w",
            pullRequestNumber: 0,
            now: Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "RepoCoordinate отвергает неизвестный provider")]
    public void Coordinate_rejects_unknown_provider()
    {
        var act = () => new RepoCoordinate("bitbucket", "o", "r");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Restore проверяет clone_status и pull_request_state")]
    public void Restore_validates_enum_like_strings()
    {
        var badStatus = () => IntentRepositoryBindingFactory.Restore(
            SnapshotWith(cloneStatus: "weird"));
        badStatus.Should().Throw<ArgumentOutOfRangeException>();

        var badPrState = () => IntentRepositoryBindingFactory.Restore(
            SnapshotWith(cloneStatus: CloneStatusNames.Ready, pullRequestNumber: 1, pullRequestState: "draft"));
        badPrState.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Restore запрещает pull_request_state без pull_request_number")]
    public void Restore_requires_pr_number_for_state()
    {
        var act = () => IntentRepositoryBindingFactory.Restore(
            SnapshotWith(
                cloneStatus: CloneStatusNames.Ready,
                pullRequestNumber: null,
                pullRequestState: PullRequestStateNames.Open));

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Restore возвращает агрегат с теми же полями")]
    public void Restore_roundtrip()
    {
        var snapshot = SnapshotWith(
            cloneStatus: CloneStatusNames.Ready,
            pullRequestNumber: 7,
            pullRequestState: PullRequestStateNames.Open,
            reviewCommentsEtag: "\"abc\"",
            lastSyncedAt: Now.AddMinutes(-5));

        var binding = IntentRepositoryBindingFactory.Restore(snapshot);

        binding.State.CloneStatus.Should().Be(CloneStatusNames.Ready);
        binding.State.PullRequestNumber.Should().Be(7);
        binding.State.PullRequestState.Should().Be(PullRequestStateNames.Open);
        binding.State.ReviewCommentsEtag.Should().Be("\"abc\"");
        binding.State.LastSyncedAt.Should().Be(Now.AddMinutes(-5));
        binding.CreatedAt.Should().Be(snapshot.CreatedAt);
        binding.State.UpdatedAt.Should().Be(snapshot.UpdatedAt);
    }

    private static IntentRepositoryBindingSnapshot SnapshotWith(
        string cloneStatus = CloneStatusNames.Pending,
        string? cloneError = null,
        int? pullRequestNumber = null,
        string? pullRequestState = null,
        string? reviewCommentsEtag = null,
        DateTimeOffset? lastSyncedAt = null) =>
        new(
            Id: BindingId.New(),
            IntentId: new IntentId("intent-abc"),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "anthropics", "throne"),
            DefaultBranch: "main",
            WorkspacePath: "/tmp/workspace",
            CloneStatus: cloneStatus,
            CloneError: cloneError,
            PullRequestNumber: pullRequestNumber,
            PullRequestState: pullRequestState,
            ReviewCommentsEtag: reviewCommentsEtag,
            LastSyncedAt: lastSyncedAt,
            CreatedAt: Now,
            UpdatedAt: Now);
}
