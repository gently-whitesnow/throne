using Microsoft.Extensions.Logging;
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
public sealed partial class PullRequestStateRefresher(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    IntentMergeAutoCloser autoCloser,
    TimeProvider clock,
    ILogger<PullRequestStateRefresher> logger)
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
            await CloseMergedAsync(binding, snapshot.State, ct);
            return binding;
        }
        LogStateChanged(
            logger,
            binding.Id.Value,
            binding.State.PullRequestNumber!.Value,
            binding.State.PullRequestState ?? "<null>",
            snapshot.State);
        binding.RecordPullRequestState(snapshot.State, clock.GetUtcNow());
        var saved = await SaveAsync(binding, ct);
        await CloseMergedAsync(saved, snapshot.State, ct);
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

    private async Task CloseMergedAsync(
        IntentRepositoryBinding binding,
        string pullRequestState,
        CancellationToken ct)
    {
        if (pullRequestState == PullRequestStateNames.Merged)
        {
            LogMergedDetected(
                logger, binding.Id.Value, binding.IntentId.Value, binding.State.PullRequestNumber!.Value);
            await autoCloser.OnBindingMergedAsync(binding, ct);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "PullRequestStateRefresher: binding {BindingId} PR #{Pr} state {OldState} -> {NewState}")]
    private static partial void LogStateChanged(
        ILogger logger, string bindingId, int pr, string oldState, string newState);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "PullRequestStateRefresher: binding {BindingId} (intent {IntentId}) PR #{Pr} observed merged "
            + "— invoking auto-closer.")]
    private static partial void LogMergedDetected(
        ILogger logger, string bindingId, string intentId, int pr);
}
