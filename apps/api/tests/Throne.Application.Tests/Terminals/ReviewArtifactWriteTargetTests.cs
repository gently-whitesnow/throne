using System.Globalization;
using FluentAssertions;
using Throne.Application.Errors;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Terminals;

public class ReviewArtifactWriteTargetTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Review target выбирается только при ровно одном attached PR")]
    public void Review_target_requires_exactly_one_attached_pull_request()
    {
        ReviewArtifactWriteTarget.Resolve(TerminalRunModes.Work, []).Should().BeNull();

        var target = ReviewArtifactWriteTarget.Resolve(TerminalRunModes.Review, [Binding(42)]);
        target.Should().Be(new ReviewArtifactWriteTarget("binding-42", 42));

        var noPr = () => ReviewArtifactWriteTarget.Resolve(TerminalRunModes.Review, [Binding(null)]);
        noPr.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.ValidationFailed);

        var many = () => ReviewArtifactWriteTarget.Resolve(
            TerminalRunModes.Review,
            [Binding(41), Binding(42)]);
        many.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static IntentRepositoryBinding Binding(int? pullRequestNumber) =>
        IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
            Id: new BindingId($"binding-{pullRequestNumber?.ToString(CultureInfo.InvariantCulture) ?? "none"}"),
            IntentId: new IntentId("intent-1"),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "repo"),
            WorkspacePath: "/tmp/repo",
            DefaultBranch: "main",
            CloneStatus: CloneStatusNames.Ready,
            CloneError: null,
            PullRequestNumber: pullRequestNumber,
            PullRequestState: null,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now));
}
