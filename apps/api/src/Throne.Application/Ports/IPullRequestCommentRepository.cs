using Throne.Application.Events;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="PullRequestCommentRecord"/>. Owned by the
/// background <c>PullRequestSyncService</c> (T-10) and reused by the manual sync
/// use-case (T-08) so both the background and manual paths share the same
/// «store new comments, fan out one event per comment» pipeline (ADR-0024 § 6).
///
/// The store is idempotent: re-storing a comment with the same
/// <c>(binding_id, upstream_id)</c> tuple is a no-op (does not raise
/// <see cref="IntentPrCommentAdded"/>). This is what makes the conditional-GET
/// loop safe to replay — a 304 means «nothing new», a fresh 200 may still echo
/// previously-stored ids.
/// </summary>
public interface IPullRequestCommentRepository
{
    /// <summary>
    /// Insert only those comments whose <c>(binding_id, upstream_id)</c> tuple is
    /// not already present. Returns the records that were actually inserted so the
    /// outcome carrier can fan out <see cref="IntentPrCommentAdded"/> per comment.
    /// </summary>
    Task<PersistPullRequestCommentsOutcome> PersistNewAsync(
        IntentRepositoryBinding binding,
        IReadOnlyList<PullRequestCommentRecord> candidates,
        CancellationToken ct);

    /// <summary>
    /// Read all stored comments for <paramref name="bindingId"/>, ordered by
    /// <c>created_at</c> ASC. Consumed by the HTTP / MCP read paths (T-11 / T-13)
    /// and by the manual-sync use-case so it can return the full feed alongside
    /// freshly-observed deltas.
    /// </summary>
    Task<IReadOnlyList<PullRequestCommentRecord>> ListByBindingAsync(
        BindingId bindingId,
        CancellationToken ct);
}

/// <summary>
/// Outcome of <see cref="IPullRequestCommentRepository.PersistNewAsync"/>. Carries
/// the freshly-inserted records (deduped against the existing store) plus the
/// full stored feed after the write, and emits one
/// <see cref="IntentPrCommentAdded"/> domain event per inserted comment.
/// </summary>
public sealed record PersistPullRequestCommentsOutcome(
    IntentRepositoryBinding Binding,
    IReadOnlyList<PullRequestCommentRecord> Inserted,
    IReadOnlyList<PullRequestCommentRecord> AllStored) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events =>
        Inserted.Select(c => (IDomainEvent)new IntentPrCommentAdded(Binding, c)).ToList();
}
