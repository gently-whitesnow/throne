using FluentAssertions;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class RepositoryBindingMcpSummaryMapperTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] InternalSummaryFieldsThatMustNotLeak =
        ["Etag", "ReviewCommentsEtag", "LastSyncedAt", "CloneError"];

    [Fact(DisplayName = "ToSummary проецирует только публичные поля и сохраняет snake_case значения статусов")]
    public void ToSummary_projects_public_fields_only()
    {
        var binding = NewBinding(
            owner: "octo",
            repo: "hello",
            cloneStatus: CloneStatusNames.Ready,
            pullRequestNumber: 7,
            pullRequestState: PullRequestStateNames.Open);

        var summary = RepositoryBindingMcpSummaryMapper.ToSummary(binding);

        summary.BindingId.Should().Be(binding.Id.Value);
        summary.Provider.Should().Be(GitProviderNames.GitHub);
        summary.Owner.Should().Be("octo");
        summary.Repo.Should().Be("hello");
        summary.DefaultBranch.Should().Be("main");
        summary.WorkspacePath.Should().Be(binding.WorkspacePath);
        summary.CloneStatus.Should().Be(CloneStatusNames.Ready);
        summary.PullRequestNumber.Should().Be(7);
        summary.PullRequestState.Should().Be(PullRequestStateNames.Open);
    }

    [Fact(DisplayName = "ToSummary не утекает clone_error / etag / last_synced_at — поля DTO покрывают только публичный контракт")]
    public void ToSummary_hides_internal_state()
    {
        var binding = NewBinding(
            cloneStatus: CloneStatusNames.Failed,
            cloneError: "clone failed: gh exit=128",
            etag: "W/\"abc\"",
            lastSyncedAt: Now);

        var summary = RepositoryBindingMcpSummaryMapper.ToSummary(binding);

        // RepositoryBindingMcpSummary intentionally has no fields for these — type itself
        // is the contract. Spot-check that nothing leaks through clone_status by surface.
        summary.CloneStatus.Should().Be(CloneStatusNames.Failed);
        summary.PullRequestNumber.Should().BeNull();
        summary.PullRequestState.Should().BeNull();
        typeof(RepositoryBindingMcpSummary).GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(InternalSummaryFieldsThatMustNotLeak);
    }

    [Fact(DisplayName = "ToSummaries для пустого списка возвращает пустую IReadOnlyList (без allocation шумов)")]
    public void ToSummaries_empty_returns_empty()
    {
        var result = RepositoryBindingMcpSummaryMapper.ToSummaries([]);
        result.Should().BeEmpty();
    }

    [Fact(DisplayName = "ToSummaries сохраняет порядок входного списка")]
    public void ToSummaries_preserves_order()
    {
        var first = NewBinding(owner: "octo", repo: "alpha");
        var second = NewBinding(owner: "octo", repo: "beta");

        var result = RepositoryBindingMcpSummaryMapper.ToSummaries([first, second]);

        result.Select(s => s.Repo).Should().Equal("alpha", "beta");
    }

    private static IntentRepositoryBinding NewBinding(
        string owner = "octo",
        string repo = "hello",
        string cloneStatus = CloneStatusNames.Pending,
        string? cloneError = null,
        int? pullRequestNumber = null,
        string? pullRequestState = null,
        string? etag = null,
        DateTimeOffset? lastSyncedAt = null)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId("intent-1"),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, owner, repo),
            WorkspacePath: $"/tmp/throne-workspaces/intents/intent-1/{owner}__{repo}",
            DefaultBranch: "main",
            CloneStatus: cloneStatus,
            CloneError: cloneError,
            PullRequestNumber: pullRequestNumber,
            PullRequestState: pullRequestState,
            ReviewCommentsEtag: etag,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: lastSyncedAt,
            CreatedAt: Now,
            UpdatedAt: Now);
        return IntentRepositoryBinding.Restore(snapshot);
    }
}
