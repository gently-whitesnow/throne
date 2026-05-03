namespace Throne.Domain.DreamRuns;

/// <summary>
/// Outcome of <see cref="DreamRun.ApplyProposal"/> / <see cref="DreamRun.SkipProposal"/>.
/// AutoClosed=true means this decision was the last pending proposal of the run, so
/// the run transitioned to <c>closed</c>.
/// </summary>
public abstract record DreamProposalDecisionResult
{
    private DreamProposalDecisionResult() { }

    public sealed record Decided(DreamProposal Proposal, bool AutoClosed) : DreamProposalDecisionResult;

    public sealed record ProposalNotFound : DreamProposalDecisionResult;

    public sealed record AlreadyDecided(DreamProposal Proposal) : DreamProposalDecisionResult;

    public sealed record RunAlreadyClosed : DreamProposalDecisionResult;
}

/// <summary>
/// Outcome of <see cref="DreamRun.Close"/>. EvidenceProcessed=false means the close
/// «released» evidence back into the unprocessed pool (typical for empty runs).
/// </summary>
public abstract record DreamRunCloseResult
{
    private DreamRunCloseResult() { }

    public sealed record Closed(bool EvidenceProcessed) : DreamRunCloseResult;

    public sealed record AlreadyClosed : DreamRunCloseResult;
}
