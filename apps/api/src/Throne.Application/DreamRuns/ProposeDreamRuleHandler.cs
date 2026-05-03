using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Domain.Instructions;

namespace Throne.Application.DreamRuns;

public sealed record ProposeDreamRuleCommand(
    string RunId,
    string TargetKind,
    string ProposedRule,
    IReadOnlyList<EvidenceRef> EvidenceRefs,
    string Rationale,
    string Severity);

public sealed record ProposeDreamRuleResult(string ProposalId, string Status);

/// <summary>
/// Proposes a single learned rule against a still-pending DreamRun. The agent
/// can only reference evidence already captured by the run snapshot — it cannot
/// invent new refs or target /throne and /dream instruction kinds (Intent 4 §propose_dream_rule).
/// </summary>
public sealed class ProposeDreamRuleHandler(
    IDreamRunRepository runs,
    IInstructionRepository instructions,
    IUnitOfWork unitOfWork)
{
    private static readonly IReadOnlyList<string> AllowedTargetKinds =
    [
        InstructionKindNames.Common,
        InstructionKindNames.Interview,
        InstructionKindNames.Work,
        InstructionKindNames.NewProject,
        InstructionKindNames.Fix,
    ];

    public async Task<ProposeDreamRuleResult> HandleAsync(ProposeDreamRuleCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateScalar(command);

        var runId = new DreamRunId(command.RunId);
        var run = await runs.GetByIdAsync(runId, ct)
            ?? throw RunNotFound(command.RunId);
        if (run.IsClosed)
        {
            throw new ApiException(
                ErrorCodes.DreamRunAlreadyClosed,
                $"DreamRun '{command.RunId}' is already closed.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId });
        }
        if (run.Proposals.Count >= DreamRun.MaxProposals)
        {
            throw new ApiException(
                ErrorCodes.DreamProposalCapReached,
                $"DreamRun '{command.RunId}' already has {DreamRun.MaxProposals} proposals (cap reached).",
                new Dictionary<string, object?>
                {
                    ["run_id"] = command.RunId,
                    ["cap"] = DreamRun.MaxProposals,
                });
        }

        var evidence = ResolveEvidence(command, run);

        var instruction = await ResolveTargetInstructionAsync(command.TargetKind, ct);

        var proposal = DreamProposal.Create(
            DreamProposalId.New(),
            instruction.Id.Value,
            command.TargetKind,
            instruction.CurrentVersion,
            command.ProposedRule.Trim(),
            evidenceSummary: BuildEvidenceSummary(evidence),
            evidence,
            command.Rationale.Trim(),
            command.Severity);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => runs.AddProposalAsync(runId, proposal, inner),
            ct);

        return outcome switch
        {
            AddDreamProposalOutcome.Added added => new ProposeDreamRuleResult(
                added.Proposal.Id.Value, DreamProposalDecisionNames.Pending),
            AddDreamProposalOutcome.RunNotFound => throw RunNotFound(command.RunId),
            AddDreamProposalOutcome.RunClosed => throw new ApiException(
                ErrorCodes.DreamRunAlreadyClosed,
                $"DreamRun '{command.RunId}' is already closed.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId }),
            AddDreamProposalOutcome.CapReached => throw new ApiException(
                ErrorCodes.DreamProposalCapReached,
                $"DreamRun '{command.RunId}' already has {DreamRun.MaxProposals} proposals (cap reached).",
                new Dictionary<string, object?>
                {
                    ["run_id"] = command.RunId,
                    ["cap"] = DreamRun.MaxProposals,
                }),
            _ => throw new InvalidOperationException($"Unhandled add-proposal outcome: {outcome.GetType().Name}"),
        };
    }

    private static void ValidateScalar(ProposeDreamRuleCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.RunId))
        {
            throw Validation("run_id must be a non-empty string.", "run_id");
        }
        if (string.IsNullOrWhiteSpace(command.TargetKind))
        {
            throw Validation("target_kind must be a non-empty string.", "target_kind");
        }
        if (!AllowedTargetKinds.Contains(command.TargetKind, StringComparer.Ordinal))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"target_kind '{command.TargetKind}' is not allowed. Use one of: {string.Join(", ", AllowedTargetKinds)}.",
                new Dictionary<string, object?>
                {
                    ["field"] = "target_kind",
                    ["allowed"] = AllowedTargetKinds,
                });
        }
        if (string.IsNullOrWhiteSpace(command.ProposedRule))
        {
            throw Validation("proposed_rule must be a non-empty string.", "proposed_rule");
        }
        if (command.ProposedRule.Length > DreamProposal.ProposedRuleMaxLength)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"proposed_rule must be at most {DreamProposal.ProposedRuleMaxLength} characters.",
                new Dictionary<string, object?>
                {
                    ["field"] = "proposed_rule",
                    ["limit"] = DreamProposal.ProposedRuleMaxLength,
                });
        }
        if (string.IsNullOrWhiteSpace(command.Rationale))
        {
            throw Validation("rationale must be a non-empty string.", "rationale");
        }
        if (string.IsNullOrWhiteSpace(command.Severity))
        {
            throw Validation("severity must be a non-empty string.", "severity");
        }
        if (!DreamProposalSeverityNames.IsKnown(command.Severity))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Unknown severity: {command.Severity}.",
                new Dictionary<string, object?>
                {
                    ["field"] = "severity",
                    ["allowed"] = DreamProposalSeverityNames.All,
                });
        }
        ArgumentNullException.ThrowIfNull(command.EvidenceRefs);
    }

    private static List<EvidenceRef> ResolveEvidence(ProposeDreamRuleCommand command, DreamRun run)
    {
        if (command.EvidenceRefs.Count == 0)
        {
            throw Validation("evidence_refs must contain at least one entry.", "evidence_refs");
        }

        var allowed = run.EvidenceRefs
            .Select(r => (r.Kind, r.Id))
            .ToHashSet();
        var resolved = new List<EvidenceRef>(command.EvidenceRefs.Count);
        foreach (var refRequested in command.EvidenceRefs)
        {
            if (refRequested is null
                || string.IsNullOrWhiteSpace(refRequested.Kind)
                || string.IsNullOrWhiteSpace(refRequested.Id))
            {
                throw Validation("evidence_refs entries must have non-empty kind and id.", "evidence_refs");
            }
            if (!EvidenceKindNames.IsKnown(refRequested.Kind))
            {
                throw new ApiException(
                    ErrorCodes.ValidationFailed,
                    $"Unknown evidence kind: {refRequested.Kind}.",
                    new Dictionary<string, object?>
                    {
                        ["field"] = "evidence_refs",
                        ["allowed"] = EvidenceKindNames.All,
                    });
            }
            if (!allowed.Contains((refRequested.Kind, refRequested.Id)))
            {
                throw new ApiException(
                    ErrorCodes.DreamProposalEvidenceUnknown,
                    "evidence_refs must be a subset of run.EvidenceRefs.",
                    new Dictionary<string, object?>
                    {
                        ["run_id"] = run.Id.Value,
                        ["unknown_kind"] = refRequested.Kind,
                        ["unknown_id"] = refRequested.Id,
                    });
            }
            resolved.Add(EvidenceRef.Create(refRequested.Kind, refRequested.Id, refRequested.CreatedAt));
        }
        var minimum = command.Severity switch
        {
            DreamProposalSeverityNames.High => 1,
            DreamProposalSeverityNames.Medium => 2,
            DreamProposalSeverityNames.Low => 3,
            _ => 1,
        };
        if (resolved.Count < minimum)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Severity '{command.Severity}' requires at least {minimum} evidence ref(s); got {resolved.Count}.",
                new Dictionary<string, object?>
                {
                    ["field"] = "evidence_refs",
                    ["severity"] = command.Severity,
                    ["minimum"] = minimum,
                });
        }
        return resolved;
    }

    private async Task<Instruction> ResolveTargetInstructionAsync(string targetKind, CancellationToken ct)
    {
        var matches = await instructions.GetUserInstructionsByKindsAsync(MvpUser.Id, [targetKind], ct);
        if (matches.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"User instruction with kind '{targetKind}' not found for current user.",
                new Dictionary<string, object?>
                {
                    ["kind"] = targetKind,
                    ["user_id"] = MvpUser.Id,
                });
        }
        return matches[0];
    }

    private static string BuildEvidenceSummary(List<EvidenceRef> refs)
    {
        if (refs.Count == 0)
        {
            return "no_refs";
        }
        return string.Join(",", refs
            .GroupBy(r => r.Kind, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => $"{g.Key}:{g.Count()}"));
    }

    private static ApiException Validation(string detail, string field) => new(
        ErrorCodes.ValidationFailed,
        detail,
        new Dictionary<string, object?> { ["field"] = field });

    private static ApiException RunNotFound(string runId) => new(
        ErrorCodes.DreamRunNotFound,
        $"DreamRun '{runId}' not found.",
        new Dictionary<string, object?> { ["run_id"] = runId });
}
