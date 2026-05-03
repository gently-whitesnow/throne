namespace Throne.Domain.DreamRuns;

/// <summary>
/// Embedded child of <see cref="DreamRun"/>. A pending proposal lives ONLY here —
/// applying it produces an ordinary <c>TextVersion</c> on the target instruction
/// (see ADR-0011). Decision transitions are linear: pending → applied | skipped.
/// </summary>
public sealed class DreamProposal
{
    public const int ProposedRuleMaxLength = 280;
    public const int RejectedReasonMinLength = 5;

    private DreamProposal(
        DreamProposalId id,
        string targetInstructionId,
        string targetKind,
        int baseInstructionVersion,
        string proposedRule,
        string evidenceSummary,
        IReadOnlyList<IntentRef> intentRefs,
        string rationale,
        string severity,
        string decision,
        string? finalRule,
        int? appliedInstructionVersion,
        string? rejectedReason)
    {
        Id = id;
        TargetInstructionId = targetInstructionId;
        TargetKind = targetKind;
        BaseInstructionVersion = baseInstructionVersion;
        ProposedRule = proposedRule;
        EvidenceSummary = evidenceSummary;
        IntentRefs = intentRefs;
        Rationale = rationale;
        Severity = severity;
        Decision = decision;
        FinalRule = finalRule;
        AppliedInstructionVersion = appliedInstructionVersion;
        RejectedReason = rejectedReason;
    }

    public DreamProposalId Id { get; }
    public string TargetInstructionId { get; }
    public string TargetKind { get; }
    public int BaseInstructionVersion { get; }
    public string ProposedRule { get; }
    public string EvidenceSummary { get; }
    public IReadOnlyList<IntentRef> IntentRefs { get; }
    public string Rationale { get; }
    public string Severity { get; }
    public string Decision { get; private set; }
    public string? FinalRule { get; private set; }
    public int? AppliedInstructionVersion { get; private set; }
    public string? RejectedReason { get; private set; }

    public bool IsPending => string.Equals(Decision, DreamProposalDecisionNames.Pending, StringComparison.Ordinal);

    public static DreamProposal Create(
        DreamProposalId id,
        string targetInstructionId,
        string targetKind,
        int baseInstructionVersion,
        string proposedRule,
        string evidenceSummary,
        IReadOnlyList<IntentRef> intentRefs,
        string rationale,
        string severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetInstructionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposedRule);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
        ArgumentException.ThrowIfNullOrWhiteSpace(severity);
        ArgumentNullException.ThrowIfNull(intentRefs);
        if (proposedRule.Length > ProposedRuleMaxLength)
        {
            throw new ArgumentException(
                $"proposed_rule must be at most {ProposedRuleMaxLength} characters.",
                nameof(proposedRule));
        }
        if (baseInstructionVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseInstructionVersion), "base_instruction_version must be >= 1.");
        }
        if (!DreamProposalSeverityNames.IsKnown(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), $"Unknown severity: {severity}.");
        }
        ValidateSeverityIntents(severity, intentRefs);

        return new DreamProposal(
            id,
            targetInstructionId,
            targetKind,
            baseInstructionVersion,
            proposedRule,
            evidenceSummary,
            [.. intentRefs],
            rationale,
            severity,
            DreamProposalDecisionNames.Pending,
            finalRule: null,
            appliedInstructionVersion: null,
            rejectedReason: null);
    }

    public static DreamProposal Restore(
        DreamProposalId id,
        string targetInstructionId,
        string targetKind,
        int baseInstructionVersion,
        string proposedRule,
        string evidenceSummary,
        IReadOnlyList<IntentRef> intentRefs,
        string rationale,
        string severity,
        string decision,
        string? finalRule,
        int? appliedInstructionVersion,
        string? rejectedReason)
    {
        if (!DreamProposalDecisionNames.IsKnown(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision), $"Unknown decision: {decision}.");
        }
        if (!DreamProposalSeverityNames.IsKnown(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), $"Unknown severity: {severity}.");
        }

        return new DreamProposal(
            id, targetInstructionId, targetKind, baseInstructionVersion,
            proposedRule, evidenceSummary, [.. intentRefs], rationale, severity,
            decision, finalRule, appliedInstructionVersion, rejectedReason);
    }

    internal void MarkApplied(string finalRule, int appliedInstructionVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalRule);
        ArgumentOutOfRangeException.ThrowIfLessThan(appliedInstructionVersion, 1);
        Decision = DreamProposalDecisionNames.Applied;
        FinalRule = finalRule;
        AppliedInstructionVersion = appliedInstructionVersion;
    }

    internal void MarkSkipped(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (reason.Trim().Length < RejectedReasonMinLength)
        {
            throw new ArgumentException(
                $"reason must be at least {RejectedReasonMinLength} characters.", nameof(reason));
        }
        Decision = DreamProposalDecisionNames.Skipped;
        RejectedReason = reason;
    }

    private static void ValidateSeverityIntents(string severity, IReadOnlyList<IntentRef> refs)
    {
        var minimum = severity switch
        {
            DreamProposalSeverityNames.High => 1,
            DreamProposalSeverityNames.Medium => 2,
            DreamProposalSeverityNames.Low => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(severity)),
        };
        var distinct = refs.Select(r => r.IntentId).Distinct(StringComparer.Ordinal).Count();
        if (distinct < minimum)
        {
            throw new ArgumentException(
                $"Severity '{severity}' requires at least {minimum} distinct intent ref(s); got {distinct}.",
                nameof(refs));
        }
    }
}
