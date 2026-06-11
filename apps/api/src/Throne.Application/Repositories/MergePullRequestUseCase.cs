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

    /// <param name="autoCompleteSession">
    /// When false, the binding is flagged to skip auto-close-on-merge before the provider
    /// merge is issued, so the intent stays open (and its agent session alive) once the sync
    /// tick observes the merge. The flag is persisted first on purpose: it must be durable
    /// before the PR can flip to <c>merged</c> upstream, otherwise a sync tick racing the
    /// merge could close the intent. Default (true) leaves the binding untouched.
    /// </param>
    public async Task<PullRequestMergeResult> MergeAsync(
        IntentRepositoryBinding binding,
        MergePullRequestRequest request,
        bool autoCompleteSession,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(request);
        if (!autoCompleteSession && !binding.State.SuppressMergeAutoClose)
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
