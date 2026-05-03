namespace Throne.Domain.DreamRuns;

/// <summary>
/// Aggregate root of the «dream» process (ADR-0011). One DreamRun captures a frozen
/// window of Intents whose qa/review activity is ready to feed /dream learning,
/// together with the embedded proposals an agent made on top of that snapshot.
/// </summary>
public sealed class DreamRun
{
    public const int MaxProposals = 5;

    private readonly List<IntentRef> _intentRefs;
    private readonly List<DreamProposal> _proposals;

    private DreamRun(
        DreamRunId id,
        string status,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        int tokenCount,
        IReadOnlyList<IntentRef> intentRefs,
        IReadOnlyList<DreamProposal> proposals,
        DateTimeOffset createdAt,
        DateTimeOffset? closedAt,
        bool evidenceProcessed)
    {
        Id = id;
        Status = status;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        TokenCount = tokenCount;
        _intentRefs = [.. intentRefs];
        _proposals = [.. proposals];
        CreatedAt = createdAt;
        ClosedAt = closedAt;
        EvidenceProcessed = evidenceProcessed;
    }

    public DreamRunId Id { get; }
    public string Status { get; private set; }
    public DateTimeOffset WindowStart { get; }
    public DateTimeOffset WindowEnd { get; }
    public int TokenCount { get; }
    public IReadOnlyList<IntentRef> IntentRefs => _intentRefs;
    public IReadOnlyList<DreamProposal> Proposals => _proposals;
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>
    /// True when this closed run «consumed» its intents — referenced intents will not
    /// resurface in the next readiness calculation. False when the run was discarded
    /// (default for empty manual close, or explicit release_evidence=true).
    /// </summary>
    public bool EvidenceProcessed { get; private set; }

    public bool IsClosed => string.Equals(Status, DreamRunStatusNames.Closed, StringComparison.Ordinal);

    public static DreamRun Create(
        DreamRunId id,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        int tokenCount,
        IReadOnlyList<IntentRef> intentRefs,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(intentRefs);
        if (windowEnd <= windowStart)
        {
            throw new ArgumentException("WindowEnd must be strictly after WindowStart.", nameof(windowEnd));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(tokenCount);
        if (intentRefs.Count == 0)
        {
            throw new ArgumentException("DreamRun must reference at least one intent.", nameof(intentRefs));
        }
        var distinct = intentRefs.Select(r => r.IntentId).Distinct(StringComparer.Ordinal).Count();
        if (distinct != intentRefs.Count)
        {
            throw new ArgumentException("intent_refs must be distinct by intent_id.", nameof(intentRefs));
        }

        return new DreamRun(
            id,
            DreamRunStatusNames.Pending,
            windowStart,
            windowEnd,
            tokenCount,
            intentRefs,
            proposals: [],
            createdAt: now,
            closedAt: null,
            evidenceProcessed: false);
    }

    public static DreamRun Restore(
        DreamRunId id,
        string status,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        int tokenCount,
        IReadOnlyList<IntentRef> intentRefs,
        IReadOnlyList<DreamProposal> proposals,
        DateTimeOffset createdAt,
        DateTimeOffset? closedAt,
        bool evidenceProcessed)
    {
        if (!DreamRunStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), $"Unknown DreamRun status: {status}.");
        }
        return new DreamRun(
            id, status, windowStart, windowEnd, tokenCount, intentRefs,
            proposals, createdAt, closedAt, evidenceProcessed);
    }

    public DreamProposal? FindProposal(DreamProposalId proposalId) =>
        _proposals.FirstOrDefault(p => string.Equals(p.Id.Value, proposalId.Value, StringComparison.Ordinal));

    /// <summary>
    /// Adds a proposal under the run's guardrails. Throws if the run is closed
    /// or the per-run cap has been reached.
    /// </summary>
    public void AddProposal(DreamProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (IsClosed)
        {
            throw new InvalidOperationException("Cannot add a proposal to a closed DreamRun.");
        }
        if (_proposals.Count >= MaxProposals)
        {
            throw new InvalidOperationException(
                $"DreamRun proposals cap reached ({MaxProposals}).");
        }
        if (FindProposal(proposal.Id) is not null)
        {
            throw new InvalidOperationException($"Proposal '{proposal.Id.Value}' already exists.");
        }
        _proposals.Add(proposal);
    }

    public DreamProposalDecisionResult ApplyProposal(
        DreamProposalId proposalId,
        string finalRule,
        int appliedInstructionVersion,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalRule);
        if (IsClosed)
        {
            return new DreamProposalDecisionResult.RunAlreadyClosed();
        }
        var proposal = FindProposal(proposalId);
        if (proposal is null)
        {
            return new DreamProposalDecisionResult.ProposalNotFound();
        }
        if (!proposal.IsPending)
        {
            return new DreamProposalDecisionResult.AlreadyDecided(proposal);
        }

        proposal.MarkApplied(finalRule, appliedInstructionVersion);
        var autoClosed = AutoCloseIfAllDecided(now, evidenceProcessed: true);
        return new DreamProposalDecisionResult.Decided(proposal, autoClosed);
    }

    public DreamProposalDecisionResult SkipProposal(
        DreamProposalId proposalId,
        string reason,
        DateTimeOffset now)
    {
        if (IsClosed)
        {
            return new DreamProposalDecisionResult.RunAlreadyClosed();
        }
        var proposal = FindProposal(proposalId);
        if (proposal is null)
        {
            return new DreamProposalDecisionResult.ProposalNotFound();
        }
        if (!proposal.IsPending)
        {
            return new DreamProposalDecisionResult.AlreadyDecided(proposal);
        }

        proposal.MarkSkipped(reason);
        var autoClosed = AutoCloseIfAllDecided(now, evidenceProcessed: true);
        return new DreamProposalDecisionResult.Decided(proposal, autoClosed);
    }

    public DreamRunCloseResult Close(bool? releaseEvidenceOverride, DateTimeOffset now)
    {
        if (IsClosed)
        {
            return new DreamRunCloseResult.AlreadyClosed();
        }

        // По умолчанию пустой run «отпускает» intents (count==0); run с proposals — фиксирует.
        var defaultRelease = _proposals.Count == 0;
        var release = releaseEvidenceOverride ?? defaultRelease;
        var processed = !release;

        Status = DreamRunStatusNames.Closed;
        ClosedAt = now;
        EvidenceProcessed = processed;
        if (release)
        {
            _intentRefs.Clear();
        }
        return new DreamRunCloseResult.Closed(processed);
    }

    public int AppliedCount => _proposals.Count(p =>
        string.Equals(p.Decision, DreamProposalDecisionNames.Applied, StringComparison.Ordinal));

    public int SkippedCount => _proposals.Count(p =>
        string.Equals(p.Decision, DreamProposalDecisionNames.Skipped, StringComparison.Ordinal));

    public int PendingCount => _proposals.Count(p => p.IsPending);

    private bool AutoCloseIfAllDecided(DateTimeOffset now, bool evidenceProcessed)
    {
        if (_proposals.Count == 0 || _proposals.Any(p => p.IsPending))
        {
            return false;
        }
        Status = DreamRunStatusNames.Closed;
        ClosedAt = now;
        EvidenceProcessed = evidenceProcessed;
        return true;
    }
}
