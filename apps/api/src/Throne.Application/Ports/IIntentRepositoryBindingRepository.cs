using Throne.Application.Events;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="IntentRepositoryBinding"/> (ADR-0024).
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
    /// All bindings attached to an intent, ordered by <c>created_at</c> ASC.
    /// </summary>
    Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(IntentId intentId, CancellationToken ct);

    /// <summary>
    /// All bindings where <c>clone_status == ready</c>, a PR is attached, and the recorded
    /// upstream PR state is <c>open</c>. Ordered by <c>last_synced_at ASC</c> so the
    /// oldest-polled binding goes first; bindings without a recorded sync
    /// (<c>last_synced_at == null</c>) sort to the head of the list.
    /// </summary>
    Task<IReadOnlyList<IntentRepositoryBinding>> FindOpenForSyncAsync(CancellationToken ct);

    /// <summary>
    /// All bindings currently parked in <paramref name="cloneStatus"/>. Used by startup
    /// recovery: <c>pending</c> re-queues work the process crashed before consuming;
    /// <c>cloning</c> flips mid-flight clones to <c>failed("interrupted")</c>
    /// per ADR-0024 § 5.
    /// </summary>
    Task<IReadOnlyList<IntentRepositoryBinding>> FindByCloneStatusAsync(
        string cloneStatus,
        CancellationToken ct);

    /// <summary>
    /// Overwrite the mutable <see cref="IntentRepositoryBinding.State"/> snapshot for an
    /// existing binding. Identity (intent / coordinate / workspace path) is immutable and
    /// is not re-written.
    /// </summary>
    Task<SaveBindingOutcome> SaveAsync(IntentRepositoryBinding binding, CancellationToken ct);

    /// <summary>
    /// Atomically claim a binding for cloning: flip <c>pending → cloning</c> only while the
    /// <em>stored</em> <c>clone_status</c> is still <c>pending</c> (compare-and-swap). The
    /// same binding can land in the clone queue twice from a single Run pre-flight — once
    /// from <c>RunPreflightAutoBind</c> (bind auto-enqueues) and again from
    /// <c>RunPreflightCloneScheduler</c> — and the queue is drained concurrently. Without
    /// this CAS both workers read <c>pending</c> and both start <c>gh clone</c> into the same
    /// workspace path, so the second fails with «destination path already exists». The loser
    /// gets <see cref="SaveBindingOutcome.NotFound"/> (the row is gone <em>or</em> already
    /// past <c>pending</c>) and must skip the clone. <paramref name="binding"/> already
    /// carries the <c>cloning</c> state; the precondition is checked against the DB value.
    /// </summary>
    Task<SaveBindingOutcome> ClaimCloningAsync(IntentRepositoryBinding binding, CancellationToken ct);

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
