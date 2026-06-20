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

    [Fact(DisplayName = "Review target выбирает единственный attached PR без явного выбора")]
    public void Review_target_selects_single_attached_pull_request()
    {
        ReviewArtifactWriteTarget.Resolve(TerminalRunModes.Work, null, []).Should().BeNull();

        var target = ReviewArtifactWriteTarget.Resolve(TerminalRunModes.Review, null, [Binding(42)]);
        target.Should().Be(new ReviewArtifactWriteTarget("binding-42", 42));

        var noPr = () => ReviewArtifactWriteTarget.Resolve(TerminalRunModes.Review, null, [Binding(null)]);
        noPr.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact(DisplayName = "Review target при нескольких PR требует attached binding_id")]
    public void Review_target_uses_selected_attached_pull_request()
    {
        var target = ReviewArtifactWriteTarget.Resolve(
            TerminalRunModes.Review,
            "binding-42",
            [Binding(41), Binding(42)]);
        target.Should().Be(new ReviewArtifactWriteTarget("binding-42", 42));

        var missingSelection = () => ReviewArtifactWriteTarget.Resolve(
            TerminalRunModes.Review,
            null,
            [Binding(41), Binding(42)]);
        missingSelection.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.ValidationFailed);

        var detachedSelection = () => ReviewArtifactWriteTarget.Resolve(
            TerminalRunModes.Review,
            "binding-99",
            [Binding(41), Binding(42)]);
        detachedSelection.Should().Throw<ApiException>().Which.Code.Should().Be(ErrorCodes.ValidationFailed);
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
