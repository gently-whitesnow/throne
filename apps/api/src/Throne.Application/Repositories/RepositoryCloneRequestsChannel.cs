using System.Threading.Channels;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// In-process channel feeding the background <c>RepositoryCloneService</c>.
/// Deliberately in-memory and unbounded — the work is rare (clone-on-bind) and
/// a process restart simply replays everything via the recovery pass.
/// ADR-0024 § 5 will revisit persistence if the workload grows.
/// </summary>
public sealed class RepositoryCloneRequestsChannel : IRepositoryCloneRequests, IRepositoryCloneRequestsReader
{
    private readonly Channel<BindingId> _channel = Channel.CreateUnbounded<BindingId>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask EnqueueAsync(BindingId bindingId, CancellationToken ct) =>
        _channel.Writer.WriteAsync(bindingId, ct);

    public IAsyncEnumerable<BindingId> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}

/// <summary>
/// Read-side seam for the background <c>RepositoryCloneService</c> worker. Kept
/// separate from <see cref="IRepositoryCloneRequests"/> so producers cannot
/// accidentally consume the queue and so the worker can be unit-tested with a
/// hand-rolled async enumerator.
/// </summary>
public interface IRepositoryCloneRequestsReader
{
    IAsyncEnumerable<BindingId> ReadAllAsync(CancellationToken ct);
}
