using Throne.Application.Events;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Carrier for the «PR attached to an existing binding» write (auto-bind pass A + the manual
/// attach endpoint C). Re-raises <see cref="IntentRepositoryBound"/> with the refreshed
/// binding so the card invalidates its repository list and shows the freshly linked PR — no
/// dedicated «binding updated» realtime event is introduced (reuses the existing wire shape).
/// <see cref="Persisted"/> is <see langword="false"/> when the binding vanished between read
/// and save (lost a concurrent unbind), suppressing the event.
/// </summary>
public sealed record RepositoryPullRequestAttachedResult(
    IntentRepositoryBinding Binding,
    bool Persisted) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events =>
        Persisted ? [new IntentRepositoryBound(Binding)] : [];
}
