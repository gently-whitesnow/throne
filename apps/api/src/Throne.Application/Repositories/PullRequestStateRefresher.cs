using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Refreshes upstream PR state and persists the delta so bindings that flip to
/// <c>closed</c>/<c>merged</c> drop out of the next <c>FindOpenForSync</c> tick. On the
/// transition into <c>merged</c> it hands off to <see cref="IntentMergeAutoCloser"/>, which
/// closes the intent once all its PR-bearing bindings are merged (intent spec B / Q6).
/// </summary>
public sealed class PullRequestStateRefresher(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    IntentMergeAutoCloser autoCloser,
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
        var saved = await SaveAsync(binding, ct);
        if (snapshot.State == PullRequestStateNames.Merged)
        {
            await autoCloser.OnBindingMergedAsync(saved, ct);
        }
        return saved;
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
