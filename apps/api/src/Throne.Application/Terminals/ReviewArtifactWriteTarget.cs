using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed record ReviewArtifactWriteTarget(string BindingId, int PullRequestNumber)
{
    public const string ArtifactType = "review_recommendation";

    public static ReviewArtifactWriteTarget? Resolve(
        string mode,
        string? selectedBindingId,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        if (!string.Equals(mode, TerminalRunModes.Review, StringComparison.Ordinal))
        {
            return null;
        }

        var attached = bindings
            .Where(b => b.State.PullRequestNumber is not null)
            .ToArray();
        if (attached.Length == 0)
        {
            throw TerminalFailures.ReviewRequiresPullRequest(mode, attached.Length);
        }

        var binding = selectedBindingId is null
            ? ResolveImplicit(mode, attached)
            : ResolveSelected(mode, selectedBindingId, attached);
        return new ReviewArtifactWriteTarget(binding.Id.Value, binding.State.PullRequestNumber!.Value);
    }

    private static IntentRepositoryBinding ResolveImplicit(
        string mode,
        IntentRepositoryBinding[] attached)
    {
        if (attached.Length != 1)
        {
            throw TerminalFailures.ReviewRequiresPullRequest(mode, attached.Length);
        }

        return attached[0];
    }

    private static IntentRepositoryBinding ResolveSelected(
        string mode,
        string selectedBindingId,
        IntentRepositoryBinding[] attached)
    {
        var binding = attached.SingleOrDefault(b => b.Id.Value == selectedBindingId);
        return binding ?? throw TerminalFailures.ReviewPullRequestNotAttached(
            mode, selectedBindingId, attached.Select(b => b.Id.Value).ToArray());
    }
}
