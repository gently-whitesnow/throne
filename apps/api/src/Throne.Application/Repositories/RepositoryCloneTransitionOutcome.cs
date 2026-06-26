using Throne.Application.Events;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Save-side outcome of a single clone-state transition. Implements
/// <see cref="IDomainEventCarrier"/> so the dispatching unit-of-work emits
/// <see cref="IntentRepositoryCloneProgress"/> after a successful persistence commit.
/// <see cref="Vanished"/> is the no-event branch — happens when the binding was
/// deleted between the worker's <c>GetByIdAsync</c> and <c>SaveAsync</c>.
/// </summary>
public abstract record RepositoryCloneTransitionOutcome : IDomainEventCarrier
{
    private RepositoryCloneTransitionOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    /// <summary>
    /// <c>true</c> when the transition hit a row (event emitted). For the cloning claim a
    /// <c>false</c> here means the CAS was lost / the binding vanished — the caller must not
    /// proceed with the clone.
    /// </summary>
    public bool WasPersisted => this is PersistedOutcome;

    public static RepositoryCloneTransitionOutcome Persisted(IntentRepositoryBinding binding) =>
        new PersistedOutcome(binding);

    public static RepositoryCloneTransitionOutcome Vanished => VanishedOutcome.Instance;

    private sealed record PersistedOutcome(IntentRepositoryBinding Binding) : RepositoryCloneTransitionOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentRepositoryCloneProgress(Binding)];
    }

    private sealed record VanishedOutcome : RepositoryCloneTransitionOutcome
    {
        public static readonly VanishedOutcome Instance = new();
    }
}
