using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Hand-off seam between <see cref="RepositoryBindingService"/> and the background
/// <c>RepositoryCloneService</c>. A successful <c>Bind</c> creates a binding in
/// <c>pending</c> and enqueues its <see cref="BindingId"/> here — the worker consumes the
/// queue, runs <c>gh repo clone</c> and transitions the binding to <c>cloning → ready/failed</c>.
/// One-directional (push only): the service knows nothing about channels, hosted services
/// or backpressure. ADR-0024 § 5 defines the queue contract.
/// </summary>
public interface IRepositoryCloneRequests
{
    /// <summary>
    /// Enqueue a freshly created binding for cloning. Must be called after the
    /// persistence write commits — otherwise the worker could pick the id up before the
    /// binding is visible in Mongo. The implementation is non-blocking.
    /// </summary>
    ValueTask EnqueueAsync(BindingId bindingId, CancellationToken ct);
}
