using FluentAssertions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using static Throne.Application.Tests.Repositories.RepositoryBindingTestData;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Attach-PR к готовому биндингу: помимо записи номера PR сервис переключает уже
/// склонированное дерево на этот PR (иначе оператор увидит upstream-дефолт). Вынесено из
/// <see cref="RepositoryBindingServiceTests"/>, чтобы тот класс остался в бюджете публичных
/// членов (.quality/maintainability-budget.json).
/// </summary>
public class RepositoryBindingServiceAttachTests
{
    [Fact(DisplayName = "AttachPullRequest на готовом клоне зовёт provider.CheckoutAsync с PR")]
    public async Task Attach_pull_request_ready_clone_checks_out_pr()
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = true;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Ready);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));

        var result = await fixture.Service.AttachPullRequestAsync(
            new AttachRepositoryPullRequestCommand(IntentIdValue, binding.Id.Value, 7), CancellationToken.None);

        result.State.PullRequestNumber.Should().Be(7);
        await fixture.Provider.Received(1).CheckoutAsync(
            "octo",
            "hello",
            $"{WorkspaceRoot}/intents/{IntentIdValue}/octo__hello",
            Arg.Is<CloneCheckout>(c => c.Branch == "main" && c.PullRequestNumber == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "AttachPullRequest на неготовом клоне НЕ зовёт checkout (clone-workflow сам сделает)")]
    public async Task Attach_pull_request_not_ready_skips_checkout()
    {
        var fixture = new ServiceFixture();
        fixture.Probe.DirectoryExists = false;
        var binding = NewBinding(intentId: IntentIdValue, cloneStatus: CloneStatusNames.Pending);
        fixture.Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(binding);
        fixture.Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));

        await fixture.Service.AttachPullRequestAsync(
            new AttachRepositoryPullRequestCommand(IntentIdValue, binding.Id.Value, 7), CancellationToken.None);

        await fixture.Provider.DidNotReceive().CheckoutAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CloneCheckout>(), Arg.Any<CancellationToken>());
    }
}
