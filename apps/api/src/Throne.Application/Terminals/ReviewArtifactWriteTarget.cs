using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed record ReviewArtifactWriteTarget(string BindingId, int PullRequestNumber)
{
    public const string ArtifactType = "review_recommendation";

    public static ReviewArtifactWriteTarget? Resolve(
        string mode,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        if (!string.Equals(mode, TerminalRunModes.Review, StringComparison.Ordinal))
        {
            return null;
        }

        var attached = bindings
            .Where(b => b.State.PullRequestNumber is not null)
            .ToArray();
        if (attached.Length != 1)
        {
            throw TerminalFailures.ReviewRequiresPullRequest(mode, attached.Length);
        }

        var binding = attached[0];
        return new ReviewArtifactWriteTarget(binding.Id.Value, binding.State.PullRequestNumber!.Value);
    }
}
