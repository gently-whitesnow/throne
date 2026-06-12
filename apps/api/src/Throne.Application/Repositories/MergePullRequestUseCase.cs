using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Reads mergeability and merges a binding's PR/MR straight at the provider
/// (Slice C). Throne keeps no local merge state — a successful merge is reflected
/// on the next PR sync, which records <c>merged</c> and auto-closes the intent per
/// ADR-0024 § 9. Maps provider 404 to <see cref="RepositoryBindingFailures.UpstreamGone"/>
/// and a provider refusal to <see cref="RepositoryBindingFailures.MergeRejected"/>.
/// </summary>
public sealed class MergePullRequestUseCase(
    IGitProviderRegistry providers,
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public Task<PullRequestMergeStatus> GetStatusAsync(IntentRepositoryBinding binding, CancellationToken ct) =>
        RunAsync(
            binding,
            async (provider, owner, repo, number) =>
                await provider.GetPullRequestMergeStatusAsync(owner, repo, number, ct)
                ?? throw RepositoryBindingFailures.UpstreamGone(binding));

    /// <param name="suppressAutoClose">
    /// Owns only the auto-close-on-merge decision (D2). When true the binding is flagged before the
    /// provider merge fires, so once the sync tick observes the merge the intent is left open (and
    /// its agent session alive) instead of being auto-closed to <c>done</c>. When false (default)
    /// the binding is left untouched and the merge auto-closes the intent as before. The
    /// teardown-on-done gate (<c>CleanupLocalStateOnDone</c>) is no longer touched here — it is
    /// owned by the intent and edited via the intents cleanup-on-done endpoint.
    /// </param>
    public async Task<PullRequestMergeResult> MergeAsync(
        IntentRepositoryBinding binding,
        MergePullRequestRequest request,
        bool suppressAutoClose,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);
        if (suppressAutoClose && !binding.State.SuppressMergeAutoClose)
        {
            binding.SuppressMergeAutoClose(clock.GetUtcNow());
            await unitOfWork.ExecuteAsync(inner => bindings.SaveAsync(binding, inner), ct);
        }
        return await RunAsync(
            binding,
            (provider, owner, repo, number) =>
                provider.MergePullRequestAsync(owner, repo, number, request, ct));
    }

    private async Task<T> RunAsync<T>(
        IntentRepositoryBinding binding,
        Func<IGitProvider, string, string, int, Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);

        try
        {
            return await action(
                provider,
                binding.Coordinate.Owner,
                binding.Coordinate.Repo,
                binding.State.PullRequestNumber.Value);
        }
        catch (GitProviderException ex) when (ex.Kind == GitProviderErrorKind.NotFound)
        {
            throw RepositoryBindingFailures.UpstreamGone(binding);
        }
        catch (GitProviderException ex) when (ex.Kind == GitProviderErrorKind.MergeNotAllowed)
        {
            throw RepositoryBindingFailures.MergeRejected(binding, ex);
        }
    }
}
