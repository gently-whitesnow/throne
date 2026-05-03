using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.DreamRuns;

public sealed record ApplyDreamProposalCommand(string RunId, string ProposalId, string? FinalRule);

public sealed class ApplyDreamProposalHandler(
    IDreamRunRepository runs,
    IInstructionRepository instructions,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<DreamRun> HandleAsync(ApplyDreamProposalCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var runId = new DreamRunId(command.RunId);
        var proposalId = new DreamProposalId(command.ProposalId);

        var run = await runs.GetByIdAsync(runId, ct)
            ?? throw RunNotFound(command.RunId);
        if (run.IsClosed)
        {
            throw new ApiException(
                ErrorCodes.DreamRunAlreadyClosed,
                $"DreamRun '{command.RunId}' is already closed.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId });
        }
        var proposal = run.FindProposal(proposalId)
            ?? throw ProposalNotFound(command.RunId, command.ProposalId);
        if (!proposal.IsPending)
        {
            throw new ApiException(
                ErrorCodes.DreamProposalAlreadyDecided,
                $"Proposal '{command.ProposalId}' has decision '{proposal.Decision}'.",
                new Dictionary<string, object?>
                {
                    ["run_id"] = command.RunId,
                    ["proposal_id"] = command.ProposalId,
                    ["current_decision"] = proposal.Decision,
                });
        }

        var rule = string.IsNullOrWhiteSpace(command.FinalRule) ? proposal.ProposedRule : command.FinalRule!.Trim();
        if (rule.Length > DreamProposal.ProposedRuleMaxLength)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"final_rule must be at most {DreamProposal.ProposedRuleMaxLength} characters.",
                new Dictionary<string, object?> { ["field"] = "final_rule" });
        }

        var instructionId = new InstructionId(proposal.TargetInstructionId);
        var instruction = await instructions.GetByIdAsync(instructionId, ct)
            ?? throw InstructionMissing(proposal.TargetInstructionId);
        if (instruction.CurrentVersion != proposal.BaseInstructionVersion)
        {
            throw NeedsRebase(command, proposal.BaseInstructionVersion, instruction.CurrentVersion);
        }

        var newText = LearnedRulesInjector.Inject(instruction.Text, rule);
        var oldText = instruction.Text;
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync<ApplyDreamProposalOutcome>(async inner =>
        {
            var replaceOutcome = await instructions.ReplaceTextAsync(
                instructionId,
                proposal.BaseInstructionVersion,
                oldText,
                newText,
                TextVersionAuthor.User,
                now,
                inner);

            var appliedVersion = replaceOutcome switch
            {
                ReplaceInstructionTextOutcome.Replaced replaced => replaced.Instruction.CurrentVersion,
                ReplaceInstructionTextOutcome.VersionConflict vc =>
                    throw NeedsRebase(command, proposal.BaseInstructionVersion, vc.CurrentVersion),
                ReplaceInstructionTextOutcome.NotFound =>
                    throw InstructionMissing(proposal.TargetInstructionId),
                _ => throw new InvalidOperationException(
                    $"Unhandled instruction replace outcome: {replaceOutcome.GetType().Name}"),
            };

            return await runs.ApplyProposalAsync(runId, proposalId, rule, appliedVersion, now, ct);
        }, ct);

        return outcome switch
        {
            ApplyDreamProposalOutcome.Applied applied => applied.Run,
            ApplyDreamProposalOutcome.RunNotFound => throw RunNotFound(command.RunId),
            ApplyDreamProposalOutcome.ProposalNotFound => throw ProposalNotFound(command.RunId, command.ProposalId),
            ApplyDreamProposalOutcome.RunAlreadyClosed => throw new ApiException(
                ErrorCodes.DreamRunAlreadyClosed,
                $"DreamRun '{command.RunId}' is already closed.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId }),
            ApplyDreamProposalOutcome.AlreadyDecided ad => throw new ApiException(
                ErrorCodes.DreamProposalAlreadyDecided,
                $"Proposal '{command.ProposalId}' has decision '{ad.CurrentDecision}'.",
                new Dictionary<string, object?>
                {
                    ["run_id"] = command.RunId,
                    ["proposal_id"] = command.ProposalId,
                    ["current_decision"] = ad.CurrentDecision,
                }),
            _ => throw new InvalidOperationException($"Unhandled apply outcome: {outcome.GetType().Name}"),
        };
    }

    private static ApiException RunNotFound(string runId) => new(
        ErrorCodes.DreamRunNotFound,
        $"DreamRun '{runId}' not found.",
        new Dictionary<string, object?> { ["run_id"] = runId });

    private static ApiException ProposalNotFound(string runId, string proposalId) => new(
        ErrorCodes.DreamProposalNotFound,
        $"Proposal '{proposalId}' not found in DreamRun '{runId}'.",
        new Dictionary<string, object?>
        {
            ["run_id"] = runId,
            ["proposal_id"] = proposalId,
        });

    private static ApiException InstructionMissing(string instructionId) => new(
        ErrorCodes.InstructionNotFound,
        $"Instruction '{instructionId}' not found.",
        new Dictionary<string, object?> { ["instruction_id"] = instructionId });

    private static ApiException NeedsRebase(ApplyDreamProposalCommand command, int baseVersion, int currentVersion) => new(
        ErrorCodes.DreamProposalNeedsRebase,
        "Instruction.current_version moved past proposal.base_instruction_version.",
        new Dictionary<string, object?>
        {
            ["run_id"] = command.RunId,
            ["proposal_id"] = command.ProposalId,
            ["base_instruction_version"] = baseVersion,
            ["current_instruction_version"] = currentVersion,
        });
}
