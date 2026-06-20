using FluentAssertions;
using Throne.Application.Errors;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Terminals;

public class ReviewArtifactWriteTargetTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Review-artifact target отсутствует только без binding")]
    public void Review_artifact_target_requires_binding_only()
    {
        ReviewArtifactWriteTarget.Resolve(null, []).Should().BeNull();

        var binding = Binding("binding-no-pr", pullRequestNumber: null);
        var target = ReviewArtifactWriteTarget.Resolve(null, [binding]);
        target.Should().Be(new ReviewArtifactWriteTarget("binding-no-pr", binding.Coordinate));
    }

    [Fact(DisplayName = "Review-artifact target может выбрать конкретный binding_id без PR-гейта")]
    public void Review_artifact_target_uses_selected_binding()
    {
        var selected = Binding("binding-42", pullRequestNumber: null);
        var target = ReviewArtifactWriteTarget.Resolve(
            "binding-42",
            [Binding("binding-41", 41), selected]);
        target.Should().Be(new ReviewArtifactWriteTarget("binding-42", selected.Coordinate));

        var detachedSelection = () => ReviewArtifactWriteTarget.Resolve(
            "binding-99",
            [Binding("binding-41", 41), selected]);
        detachedSelection.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static IntentRepositoryBinding Binding(string id, int? pullRequestNumber) =>
        IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
            Id: new BindingId(id),
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
