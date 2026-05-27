using Throne.Domain.Capabilities;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence port for the <see cref="Capabilities"/> singleton-aggregate. Implementations
/// upsert the row identified by <see cref="Capabilities.SingletonId"/> — there is never
/// more than one document in the underlying collection.
/// </summary>
public interface ICapabilitiesRepository
{
    /// <summary>
    /// Read the persisted singleton. Returns <c>null</c> when the row has never been
    /// written — callers (Application) materialise the default-off shape themselves so
    /// new capability keys can appear without a migration.
    /// </summary>
    Task<Capabilities?> GetAsync(CancellationToken ct);

    /// <summary>
    /// Persist the aggregate. Upserts the singleton row in one round-trip; expected to
    /// run inside <see cref="IUnitOfWork.ExecuteAsync(System.Func{System.Threading.CancellationToken, System.Threading.Tasks.Task}, System.Threading.CancellationToken)"/>.
    /// </summary>
    Task SaveAsync(Capabilities capabilities, CancellationToken ct);
}
