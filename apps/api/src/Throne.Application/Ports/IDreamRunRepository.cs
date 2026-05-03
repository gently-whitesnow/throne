using Throne.Application.DreamRuns;
using Throne.Application.Events;
using Throne.Domain.DreamRuns;

namespace Throne.Application.Ports;

public interface IDreamRunRepository
{
    Task<CreateDreamRunOutcome> CreateAsync(DreamRun run, CancellationToken ct);

    Task<DreamRun?> GetByIdAsync(DreamRunId id, CancellationToken ct);

    Task<IReadOnlyList<DreamRun>> ListPendingAsync(CancellationToken ct);

    Task<int> GetPendingProposalsCountAsync(CancellationToken ct);

    Task<DreamRun?> GetMostRecentClosedAsync(CancellationToken ct);

    /// <summary>
    /// Returns intent ids already consumed by closed DreamRuns whose <c>EvidenceProcessed</c>
    /// is true. Used to filter the «available» window.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetProcessedIntentIdsAsync(CancellationToken ct);

    /// <summary>
    /// Returns intent ids locked by currently pending DreamRuns. Not «available» for a fresh
    /// run but contribute to <c>locked_tokens</c> in readiness.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetLockedIntentIdsAsync(CancellationToken ct);

    Task<AddDreamProposalOutcome> AddProposalAsync(
        DreamRunId runId,
        DreamProposal proposal,
        CancellationToken ct);

    Task<ApplyDreamProposalOutcome> ApplyProposalAsync(
        DreamRunId runId,
        DreamProposalId proposalId,
        string finalRule,
        int appliedInstructionVersion,
        DateTimeOffset now,
        CancellationToken ct);

    Task<SkipDreamProposalOutcome> SkipProposalAsync(
        DreamRunId runId,
        DreamProposalId proposalId,
        string reason,
        DateTimeOffset now,
        CancellationToken ct);

    Task<CloseDreamRunOutcome> CloseAsync(
        DreamRunId runId,
        bool? releaseEvidenceOverride,
        DateTimeOffset now,
        CancellationToken ct);
}

/// <summary>
/// Read-side query over Mongo collections for assembling /dream training context.
/// Returns full per-intent payloads — text history, current text, all qa, all reviews —
/// for each intent that had qa or review activity within the safe time window.
/// </summary>
public interface IIntentWindowQueries
{
    Task<IReadOnlyList<IntentInWindow>> CollectIntentActivityAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken ct);
}

public sealed record CreateDreamRunOutcome(DreamRun Run) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => [new DreamRunCreated(Run)];
}

public abstract record AddDreamProposalOutcome : IDomainEventCarrier
{
    private AddDreamProposalOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Added(DreamRun Run, DreamProposal Proposal) : AddDreamProposalOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new DreamProposalCreated(Run, Proposal)];
    }

    public sealed record RunNotFound : AddDreamProposalOutcome;

    public sealed record RunClosed : AddDreamProposalOutcome;

    public sealed record CapReached : AddDreamProposalOutcome;
}

public abstract record ApplyDreamProposalOutcome : IDomainEventCarrier
{
    private ApplyDreamProposalOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Applied(DreamRun Run, DreamProposal Proposal, bool AutoClosed) : ApplyDreamProposalOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => AutoClosed
            ? [new DreamProposalApplied(Run, Proposal), new DreamRunClosed(Run)]
            : [new DreamProposalApplied(Run, Proposal)];
    }

    public sealed record RunNotFound : ApplyDreamProposalOutcome;

    public sealed record ProposalNotFound : ApplyDreamProposalOutcome;

    public sealed record AlreadyDecided(string CurrentDecision) : ApplyDreamProposalOutcome;

    public sealed record RunAlreadyClosed : ApplyDreamProposalOutcome;
}

public abstract record SkipDreamProposalOutcome : IDomainEventCarrier
{
    private SkipDreamProposalOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Skipped(DreamRun Run, DreamProposal Proposal, bool AutoClosed) : SkipDreamProposalOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => AutoClosed
            ? [new DreamProposalSkipped(Run, Proposal), new DreamRunClosed(Run)]
            : [new DreamProposalSkipped(Run, Proposal)];
    }

    public sealed record RunNotFound : SkipDreamProposalOutcome;

    public sealed record ProposalNotFound : SkipDreamProposalOutcome;

    public sealed record AlreadyDecided(string CurrentDecision) : SkipDreamProposalOutcome;

    public sealed record RunAlreadyClosed : SkipDreamProposalOutcome;
}

public abstract record CloseDreamRunOutcome : IDomainEventCarrier
{
    private CloseDreamRunOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Closed(DreamRun Run) : CloseDreamRunOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new DreamRunClosed(Run)];
    }

    public sealed record NotFound : CloseDreamRunOutcome;

    public sealed record AlreadyClosed : CloseDreamRunOutcome;
}
