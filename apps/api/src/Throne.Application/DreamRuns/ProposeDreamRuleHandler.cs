using Throne.Application.Auth;
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
    IReadOnlyList<string> IntentRefs,
    string Rationale,
    string Severity);

public sealed record ProposeDreamRuleResult(string ProposalId, string Status);

/// <summary>
/// Proposes a single learned rule against a still-pending DreamRun. The agent can only
/// reference intents already captured by the run snapshot — not arbitrary ones, and
/// not the dream instruction kind itself.
/// </summary>
public sealed class ProposeDreamRuleHandler(
    IDreamRunRepository runs,
    IInstructionRepository instructions,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser)
{
    private static readonly IReadOnlyList<string> AllowedTargetKinds =
    [
        InstructionKindNames.Common,
        InstructionKindNames.Interview,
        InstructionKindNames.Work,
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

        var intentRefs = ResolveIntentRefs(command, run);

        var instruction = await ResolveTargetInstructionAsync(command.TargetKind, ct);

        var proposal = DreamProposal.Create(
            DreamProposalId.New(),
            instruction.Id.Value,
            command.TargetKind,
            instruction.CurrentVersion,
            command.ProposedRule.Trim(),
            evidenceSummary: BuildEvidenceSummary(intentRefs),
            intentRefs,
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
        ArgumentNullException.ThrowIfNull(command.IntentRefs);
    }

    private static List<IntentRef> ResolveIntentRefs(ProposeDreamRuleCommand command, DreamRun run)
    {
        if (command.IntentRefs.Count == 0)
        {
            throw Validation("intent_refs must contain at least one entry.", "intent_refs");
        }

        var allowed = run.IntentRefs.ToDictionary(r => r.IntentId, r => r, StringComparer.Ordinal);
        var resolved = new List<IntentRef>(command.IntentRefs.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var intentId in command.IntentRefs)
        {
            if (string.IsNullOrWhiteSpace(intentId))
            {
                throw Validation("intent_refs entries must be non-empty intent ids.", "intent_refs");
            }
            if (!seen.Add(intentId))
            {
                throw Validation($"intent_refs has duplicate intent_id '{intentId}'.", "intent_refs");
            }
            if (!allowed.TryGetValue(intentId, out var captured))
            {
                throw new ApiException(
                    ErrorCodes.DreamProposalEvidenceUnknown,
                    "intent_refs must be a subset of run.IntentRefs.",
                    new Dictionary<string, object?>
                    {
                        ["run_id"] = run.Id.Value,
                        ["unknown_intent_id"] = intentId,
                    });
            }
            resolved.Add(captured);
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
                $"Severity '{command.Severity}' requires at least {minimum} intent ref(s); got {resolved.Count}.",
                new Dictionary<string, object?>
                {
                    ["field"] = "intent_refs",
                    ["severity"] = command.Severity,
                    ["minimum"] = minimum,
                });
        }
        return resolved;
    }

    private async Task<Instruction> ResolveTargetInstructionAsync(string targetKind, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var matches = await instructions.GetUserInstructionsByKindsAsync(userId, [targetKind], ct);
        if (matches.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"User instruction with kind '{targetKind}' not found for current user.",
                new Dictionary<string, object?>
                {
                    ["kind"] = targetKind,
                    ["user_id"] = userId,
                });
        }
        return matches[0];
    }

    private static string BuildEvidenceSummary(List<IntentRef> refs)
    {
        if (refs.Count == 0)
        {
            return "no_intents";
        }
        var totalTokens = refs.Sum(r => r.TokenCount);
        return $"intents:{refs.Count},tokens:{totalTokens}";
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
