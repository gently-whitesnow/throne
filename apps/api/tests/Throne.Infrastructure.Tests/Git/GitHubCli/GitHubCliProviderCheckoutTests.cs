using FluentAssertions;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Post-clone checkout: после клона рабочее дерево должно встать на выбранный PR/ветку,
/// а не на upstream-дефолт. PR переключается через <c>gh pr checkout</c> (тянет fork-PR),
/// ветка-override — через plain <c>git checkout</c> с guard'ами на «уже на ветке» и
/// «ref-плейсхолдер отсутствует на origin».
/// </summary>
public class GitHubCliProviderCheckoutTests
{
    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "Clone + PR → после клона зовётся gh pr checkout {n} в workspace")]
    public async Task Clone_with_pr_runs_gh_pr_checkout()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.CloneRepositoryAsync(
            "alice", "throne", "/tmp/x", new CloneCheckout(null, 7), default);

        var checkout = _fx.Calls.Single(c => c.Arguments.Contains("pr"));
        checkout.FileName.Should().Be("gh");
        checkout.WorkingDirectory.Should().Be("/tmp/x");
        checkout.Arguments.Should().BeEquivalentTo(["pr", "checkout", "7"], o => o.WithStrictOrdering());
    }

    [Fact(DisplayName = "Clone + ветка (current != target, ref есть на origin) → git checkout {branch}")]
    public async Task Clone_with_branch_runs_git_checkout()
    {
        _fx.OnRun(Branch("main", originHasRef: true));

        await _fx.Provider.CloneRepositoryAsync(
            "alice", "throne", "/tmp/x", new CloneCheckout("feature", null), default);

        var checkout = _fx.Calls.Single(c => c.FileName == "git" && c.Arguments.Contains("checkout"));
        checkout.Arguments.Should().BeEquivalentTo(
            ["-C", "/tmp/x", "checkout", "feature"], o => o.WithStrictOrdering());
    }

    [Fact(DisplayName = "Clone + ветка == текущей → git checkout НЕ вызывается")]
    public async Task Clone_with_branch_equal_to_head_skips_checkout()
    {
        _fx.OnRun(Branch("feature", originHasRef: true));

        await _fx.Provider.CloneRepositoryAsync(
            "alice", "throne", "/tmp/x", new CloneCheckout("feature", null), default);

        _fx.Calls.Should().NotContain(c => c.Arguments.Contains("checkout"));
    }

    [Fact(DisplayName = "Clone + ветка, ref отсутствует на origin → тихий no-op без checkout")]
    public async Task Clone_with_missing_origin_ref_is_silent_noop()
    {
        _fx.OnRun(Branch("main", originHasRef: false));

        await _fx.Provider.CloneRepositoryAsync(
            "alice", "throne", "/tmp/x", new CloneCheckout("feature", null), default);

        _fx.Calls.Should().NotContain(c => c.Arguments.Contains("checkout"));
    }

    [Fact(DisplayName = "CheckoutAsync на готовом клоне с PR → gh pr checkout без клонирования")]
    public async Task CheckoutAsync_with_pr_runs_gh_pr_checkout()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.CheckoutAsync(
            "alice", "throne", "/tmp/ws", new CloneCheckout(null, 5), default);

        _fx.Calls.Should().NotContain(c => c.Arguments.Contains("clone"));
        var checkout = _fx.Calls.Single();
        checkout.FileName.Should().Be("gh");
        checkout.WorkingDirectory.Should().Be("/tmp/ws");
        checkout.Arguments.Should().BeEquivalentTo(["pr", "checkout", "5"], o => o.WithStrictOrdering());
    }

    /// <summary>
    /// Factory для git-checkout пути: rev-parse HEAD → <paramref name="head"/>,
    /// rev-parse --verify origin/{branch} → success/fail по <paramref name="originHasRef"/>,
    /// остальное (clone, checkout) → Ok.
    /// </summary>
    private static Func<ProcessRunRequest, ProcessRunResult> Branch(string head, bool originHasRef) =>
        req =>
        {
            if (req.Arguments.Contains("--abbrev-ref") && req.Arguments.Contains("HEAD"))
            {
                return GitHubCliProviderFixture.Ok(head);
            }
            if (req.Arguments.Contains("--verify"))
            {
                return originHasRef
                    ? GitHubCliProviderFixture.Ok("0000000000000000000000000000000000000000")
                    : GitHubCliProviderFixture.Fail(1, string.Empty);
            }
            return GitHubCliProviderFixture.Ok(string.Empty);
        };
}
