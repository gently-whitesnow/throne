using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Hand-off seam between <see cref="RepositoryBindingService"/> (T-08) and the background
/// <c>RepositoryCloneService</c> (T-09). A successful <c>Bind</c> creates a binding in
/// <c>pending</c> and enqueues its <see cref="BindingId"/> here — the worker consumes the
/// queue, runs <c>gh repo clone</c> and transitions the binding to <c>cloning → ready/failed</c>.
///
/// The port is one-directional (push only) on purpose: the service knows nothing about
/// channels, hosted services or backpressure. Slice 1 has a single-process in-memory
/// implementation; tests use a recording fake. The name follows the analyzer-friendly
/// "<c>Requests</c>" suffix to keep CA1711 happy without re-introducing a new
/// suppression (ADR-0024 § 5 defines the queue contract regardless of the C# name).
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
