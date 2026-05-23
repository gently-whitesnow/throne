using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Helper extracted from <see cref="PullRequestSyncBindingVisitor"/> so the visitor
/// itself stays inside the per-type CA1502 cyclomatic budget. Owns the
/// «refresh upstream PR state and persist the delta» step (review-note D2 — the
/// background poller keeps <c>pull_request_state</c> fresh so bindings that flip to
/// <c>closed</c>/<c>merged</c> drop out of the next <c>FindOpenForSync</c> tick).
///
/// Also owns the single-shot <c>SaveAsync</c> helper used by both
/// <see cref="RefreshAsync"/> and the broken-marker path: keeping it here so the
/// outcome-switch contributes to this type's budget instead of the visitor's.
/// </summary>
public sealed class PullRequestStateRefresher(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IntentRepositoryBinding?> RefreshAsync(
        IntentRepositoryBinding binding,
        IGitProvider provider,
        CancellationToken ct)
    {
        var snapshot = await provider.GetPullRequestAsync(
            binding.Coordinate.Owner,
            binding.Coordinate.Repo,
            binding.State.PullRequestNumber!.Value,
            ct);
        if (snapshot is null)
        {
            return null;
        }
        if (binding.State.PullRequestState == snapshot.State)
        {
            return binding;
        }
        binding.RecordPullRequestState(snapshot.State, clock.GetUtcNow());
        return await SaveAsync(binding, ct);
    }

    public async Task MarkBrokenAsync(
        IntentRepositoryBinding binding,
        string reason,
        CancellationToken ct)
    {
        binding.MarkBroken(reason, clock.GetUtcNow());
        await SaveAsync(binding, ct);
    }

    private async Task<IntentRepositoryBinding> SaveAsync(
        IntentRepositoryBinding binding,
        CancellationToken ct)
    {
        var outcome = await unitOfWork.ExecuteAsync(
            inner => bindings.SaveAsync(binding, inner),
            ct);
        return outcome switch
        {
            SaveBindingOutcome.Saved saved => saved.Binding,
            SaveBindingOutcome.NotFound => binding,
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
