using Throne.Application.Events;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="IntentRepositoryBinding"/> (ADR-0024). T-08
/// Application services compose Create / FindByIntent / Save on top of this port; the
/// background <c>PullRequestSyncService</c> (T-10) drives polling through
/// <see cref="FindOpenForSyncAsync"/>.
///
/// Outcomes are typed so the duplicate-bind 409 and missing-binding 404 surfaces never
/// fall back on string error matching from upstream layers.
/// </summary>
public interface IIntentRepositoryBindingRepository
{
    /// <summary>
    /// Insert a freshly created binding. Returns <see cref="CreateBindingOutcome.Duplicate"/>
    /// when a binding with the same <c>(intent_id, provider, owner, repo)</c> tuple already
    /// exists — caller surfaces this as HTTP 409 (see ADR-0024 §1).
    /// </summary>
    Task<CreateBindingOutcome> CreateAsync(IntentRepositoryBinding binding, CancellationToken ct);

    Task<IntentRepositoryBinding?> GetByIdAsync(BindingId id, CancellationToken ct);

    /// <summary>
    /// All bindings attached to an intent, ordered by <c>created_at</c> ASC. Used by the
    /// HTTP <c>listIntentRepositories</c> endpoint (T-11) and the MCP
    /// <c>get_intent.repositories</c> projection (T-13).
    /// </summary>
    Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(IntentId intentId, CancellationToken ct);

    /// <summary>
    /// All bindings where <c>clone_status == ready</c>, a PR is attached, and the recorded
    /// upstream PR state is <c>open</c>. <see cref="PullRequestSyncService"/> (T-10) consumes
    /// this set and orders by <c>last_synced_at ASC</c> so the oldest-polled binding goes
    /// first. Bindings without a recorded sync (<c>last_synced_at == null</c>) sort to the
    /// head of the list.
    /// </summary>
    Task<IReadOnlyList<IntentRepositoryBinding>> FindOpenForSyncAsync(CancellationToken ct);

    /// <summary>
    /// Overwrite the mutable <see cref="IntentRepositoryBinding.State"/> snapshot for an
    /// existing binding. Identity (intent / coordinate / workspace path) is immutable and
    /// is not re-written.
    /// </summary>
    Task<SaveBindingOutcome> SaveAsync(IntentRepositoryBinding binding, CancellationToken ct);

    Task<DeleteBindingOutcome> DeleteAsync(BindingId id, CancellationToken ct);
}

public abstract record CreateBindingOutcome : IDomainEventCarrier
{
    private CreateBindingOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Created(IntentRepositoryBinding Binding) : CreateBindingOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentRepositoryBound(Binding)];
    }

    public sealed record Duplicate(IntentRepositoryBinding Existing) : CreateBindingOutcome;
}

public abstract record SaveBindingOutcome
{
    public sealed record Saved(IntentRepositoryBinding Binding) : SaveBindingOutcome;

    public sealed record NotFound : SaveBindingOutcome;
}

public abstract record DeleteBindingOutcome : IDomainEventCarrier
{
    private DeleteBindingOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Deleted(IntentRepositoryBinding Binding) : DeleteBindingOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentRepositoryUnbound(Binding)];
    }

    public sealed record NotFound : DeleteBindingOutcome;
}
