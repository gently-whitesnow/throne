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
    /// Returns evidence refs already consumed by closed DreamRuns whose
    /// <c>EvidenceProcessed</c> is true. Used to filter «available» evidence.
    /// </summary>
    Task<IReadOnlyCollection<(string Kind, string Id)>> GetProcessedEvidenceAsync(CancellationToken ct);

    /// <summary>
    /// Returns evidence refs locked by currently pending DreamRuns. These are
    /// not «available» for a fresh run but may show up as <c>locked_score</c> in readiness.
    /// </summary>
    Task<IReadOnlyCollection<(string Kind, string Id)>> GetLockedEvidenceAsync(CancellationToken ct);

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
/// Read-side queries over raw evidence sources (intent_review, intent_qa, mcp_call_log).
/// Filters by safe window + session-aware exclusion (see ADR-0011 / D).
/// </summary>
public interface IEvidenceQueries
{
    Task<IReadOnlyList<EvidenceItemRecord>> CollectAsync(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset sessionActivityCutoff,
        CancellationToken ct);
}

/// <summary>
/// Repository-level evidence record. Distinct from <see cref="DreamRuns.EvidenceItem"/>
/// to keep the Application boundary explicit.
/// </summary>
public sealed record EvidenceItemRecord(
    string Kind,
    string Id,
    DateTimeOffset CreatedAt,
    string? SessionId,
    bool HighSeverity);

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
