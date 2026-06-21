using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed record ReviewArtifactWriteTarget(string BindingId, RepoCoordinate Coordinate)
{
    public const string ArtifactType = "review_recommendation";
    public const string NoBindingReason = "нет привязанного репозитория";

    public static ReviewArtifactWriteTarget? Resolve(
        string? selectedBindingId,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        if (bindings.Count == 0)
        {
            return null;
        }

        var binding = string.IsNullOrWhiteSpace(selectedBindingId)
            ? bindings[0]
            : ResolveSelected(selectedBindingId, bindings);
        return new ReviewArtifactWriteTarget(binding.Id.Value, binding.Coordinate);
    }

    private static IntentRepositoryBinding ResolveSelected(
        string selectedBindingId,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        var binding = bindings.SingleOrDefault(b => b.Id.Value == selectedBindingId);
        return binding ?? throw TerminalFailures.ReviewPullRequestNotAttached(
            mode: "session-skill",
            selectedBindingId,
            bindings.Select(b => b.Id.Value).ToArray());
    }
}
