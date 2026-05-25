using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Per-binding step of the polling tick. State-refresh + binding persistence live in
/// <see cref="PullRequestStateRefresher"/>; this type focuses on the «is this binding
/// eligible, classify the failure» decision tree.
/// </summary>
public sealed class PullRequestSyncBindingVisitor(
    IGitProviderRegistry providers,
    RepositoryPullRequestSyncWorkflow syncWorkflow,
    PullRequestStateRefresher stateRefresher,
    PullRequestSyncBackoff backoff,
    TimeProvider clock)
{
    public async Task VisitAsync(
        IntentRepositoryBinding binding,
        PullRequestSyncTickReport report,
        CancellationToken ct)
    {
        if (backoff.ShouldSkip(binding.Id, clock.GetUtcNow()))
        {
            report.Counters.Skipped++;
            return;
        }

        var provider = providers.GetByName(binding.Coordinate.Provider);
        if (provider is null)
        {
            RecordFailure(binding, GitProviderErrorKind.CliFailure, $"provider '{binding.Coordinate.Provider}' is not registered", report);
            return;
        }

        await TryVisitAsync(binding, provider, report, ct);
    }

    private async Task TryVisitAsync(
        IntentRepositoryBinding binding,
        IGitProvider provider,
        PullRequestSyncTickReport report,
        CancellationToken ct)
    {
        try
        {
            await ExecuteAsync(binding, provider, report, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.RepositoryUpstreamGone)
        {
            backoff.Forget(binding.Id);
            report.Counters.MarkedBroken++;
        }
        catch (GitProviderException ex)
        {
            RecordFailure(binding, ex.Kind, ex.Message, report);
        }
        catch (Exception ex)
        {
            RecordFailure(binding, GitProviderErrorKind.CliFailure, ex.Message, report);
        }
    }

    private async Task ExecuteAsync(
        IntentRepositoryBinding binding,
        IGitProvider provider,
        PullRequestSyncTickReport report,
        CancellationToken ct)
    {
        var refreshed = await stateRefresher.RefreshAsync(binding, provider, ct);
        if (refreshed is null)
        {
            await stateRefresher.MarkBrokenAsync(binding, "pull request not found upstream (404)", ct);
            backoff.Forget(binding.Id);
            report.Counters.MarkedBroken++;
            return;
        }
        if (refreshed.State.PullRequestState != PullRequestStateNames.Open)
        {
            backoff.Forget(refreshed.Id);
            report.Counters.LifecycleClosed++;
            return;
        }

        var sync = await syncWorkflow.SyncAsync(refreshed, provider, ct);
        backoff.RecordSuccess(refreshed.Id);
        report.Counters.Polled++;
        report.Counters.NewComments += sync.NewComments.Count;
        if (sync.NotModified)
        {
            report.Counters.NotModified++;
        }
    }

    private void RecordFailure(
        IntentRepositoryBinding binding,
        GitProviderErrorKind kind,
        string message,
        PullRequestSyncTickReport report)
    {
        backoff.RecordFailure(binding.Id, clock.GetUtcNow());
        report.Counters.Failed++;
        report.AddFailure(new PullRequestSyncFailure(binding.Id.Value, kind, message));
    }
}
