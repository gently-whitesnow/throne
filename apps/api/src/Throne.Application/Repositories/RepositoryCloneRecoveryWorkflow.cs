using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Startup recovery pass for the clone queue (ADR-0024 § 5, parent slice Q1).
/// On every API process boot:
/// <list type="bullet">
///   <item>Every binding still in <c>cloning</c> is flipped to
///         <c>failed("interrupted")</c> through <see cref="RepositoryCloneWorkflow.MarkInterruptedAsync"/>.
///         The previous worker crashed mid-clone; the workspace directory is left
///         on disk and the user re-binds.</item>
///   <item>Every binding still in <c>pending</c> is re-pushed into
///         <see cref="IRepositoryCloneRequests"/>. Without this the in-memory
///         queue would forget any binding that was created but never consumed
///         by the worker before the crash.</item>
/// </list>
///
/// The pass is idempotent — running it twice on the same database is a no-op the
/// second time because <c>failed</c> bindings are not picked up again.
/// </summary>
public sealed class RepositoryCloneRecoveryWorkflow(
    IIntentRepositoryBindingRepository bindings,
    IRepositoryCloneRequests queue,
    RepositoryCloneWorkflow workflow)
{
    public const string InterruptedReason = "interrupted";

    public async Task<RepositoryCloneRecoveryReport> RunAsync(CancellationToken ct)
    {
        var interrupted = await FailInterruptedAsync(ct);
        var requeued = await RequeuePendingAsync(ct);
        return new RepositoryCloneRecoveryReport(interrupted, requeued);
    }

    private async Task<int> FailInterruptedAsync(CancellationToken ct)
    {
        var stuck = await bindings.FindByCloneStatusAsync(CloneStatusNames.Cloning, ct);
        foreach (var binding in stuck)
        {
            ct.ThrowIfCancellationRequested();
            _ = await workflow.MarkInterruptedAsync(binding, InterruptedReason, ct);
        }
        return stuck.Count;
    }

    private async Task<int> RequeuePendingAsync(CancellationToken ct)
    {
        var pending = await bindings.FindByCloneStatusAsync(CloneStatusNames.Pending, ct);
        foreach (var binding in pending)
        {
            ct.ThrowIfCancellationRequested();
            await queue.EnqueueAsync(binding.Id, ct);
        }
        return pending.Count;
    }
}

public sealed record RepositoryCloneRecoveryReport(int Interrupted, int Requeued);
