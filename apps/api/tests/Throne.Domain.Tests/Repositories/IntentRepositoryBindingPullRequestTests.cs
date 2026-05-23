using FluentAssertions;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

public class IntentRepositoryBindingPullRequestTests
{
    private static readonly DateTimeOffset Now = IntentRepositoryBindingTestBuilder.Now;

    [Fact(DisplayName = "AttachPullRequest валиден когда PR ещё не привязан")]
    public void Attach_when_empty()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready();

        binding.AttachPullRequest(7, Now.AddSeconds(10));

        binding.State.PullRequestNumber.Should().Be(7);
        binding.State.PullRequestState.Should().BeNull();
        binding.State.UpdatedAt.Should().Be(Now.AddSeconds(10));
    }

    [Fact(DisplayName = "AttachPullRequest второй раз бросает InvalidOperationException")]
    public void Attach_twice_throws()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready(prNumber: 1);

        var act = () => binding.AttachPullRequest(2, Now.AddSeconds(10));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "AttachPullRequest отвергает number < 1")]
    public void Attach_rejects_non_positive_number()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready();

        var act = () => binding.AttachPullRequest(0, Now.AddSeconds(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "RecordPullRequestState принимает open/closed/merged")]
    [InlineData(PullRequestStateNames.Open)]
    [InlineData(PullRequestStateNames.Closed)]
    [InlineData(PullRequestStateNames.Merged)]
    public void RecordPullRequestState_accepts_known(string state)
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready(prNumber: 1);

        binding.RecordPullRequestState(state, Now.AddSeconds(10));

        binding.State.PullRequestState.Should().Be(state);
    }

    [Fact(DisplayName = "RecordPullRequestState без привязанного PR — ошибка")]
    public void RecordPullRequestState_requires_attached_pr()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready();

        var act = () => binding.RecordPullRequestState(PullRequestStateNames.Open, Now.AddSeconds(10));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact(DisplayName = "RecordPullRequestState с неизвестным значением — ошибка")]
    public void RecordPullRequestState_rejects_unknown()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready(prNumber: 1);

        var act = () => binding.RecordPullRequestState("draft", Now.AddSeconds(10));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "RecordSync сохраняет etag и last_synced_at, обновляет updated_at")]
    public void RecordSync_persists_etag_and_timestamp()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready(prNumber: 1);
        var at = Now.AddSeconds(30);

        binding.RecordSync("\"abc123\"", at);

        binding.State.ReviewCommentsEtag.Should().Be("\"abc123\"");
        binding.State.LastSyncedAt.Should().Be(at);
        binding.State.UpdatedAt.Should().Be(at);
    }

    [Fact(DisplayName = "RecordSync с пустым etag сохраняет null, но last_synced_at пишется")]
    public void RecordSync_blank_etag_normalizes_to_null()
    {
        var binding = IntentRepositoryBindingTestBuilder.Ready(prNumber: 1);
        var at = Now.AddSeconds(30);

        binding.RecordSync("   ", at);

        binding.State.ReviewCommentsEtag.Should().BeNull();
        binding.State.LastSyncedAt.Should().Be(at);
    }
}
