using FluentAssertions;
using Throne.Application.Git;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

/// <summary>
/// Post-clone checkout для GitLab: MR переключается через <c>glab mr checkout</c> (тянет
/// fork-MR) с прокинутым <c>GITLAB_HOST</c>, иначе ветка — через plain git.
/// </summary>
public class GitLabCliProviderCheckoutTests
{
    private readonly GitLabCliProviderFixture _fx = new();

    [Fact(DisplayName = "Clone + MR → после клона зовётся glab mr checkout {n} с GITLAB_HOST")]
    public async Task Clone_with_pr_runs_glab_mr_checkout()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.CloneRepositoryAsync(
            "group/sub", "throne", "/tmp/throne", new CloneCheckout(null, 7), default);

        var checkout = _fx.Calls.Single(c => c.Arguments.Contains("mr"));
        checkout.FileName.Should().Be("glab");
        checkout.WorkingDirectory.Should().Be("/tmp/throne");
        checkout.Arguments.Should().BeEquivalentTo(["mr", "checkout", "7"], o => o.WithStrictOrdering());
        GitLabCliProviderFixture.HasGitLabHost(checkout).Should().BeTrue();
    }

    [Fact(DisplayName = "CheckoutAsync на готовом клоне с MR → glab mr checkout без клонирования")]
    public async Task CheckoutAsync_with_pr_runs_glab_mr_checkout()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.CheckoutAsync(
            "group/sub", "throne", "/tmp/ws", new CloneCheckout(null, 9), default);

        _fx.Calls.Should().NotContain(c => c.Arguments.Contains("clone"));
        var checkout = _fx.Calls.Single();
        checkout.FileName.Should().Be("glab");
        checkout.WorkingDirectory.Should().Be("/tmp/ws");
        checkout.Arguments.Should().BeEquivalentTo(["mr", "checkout", "9"], o => o.WithStrictOrdering());
        GitLabCliProviderFixture.HasGitLabHost(checkout).Should().BeTrue();
    }
}
