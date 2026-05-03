using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

public sealed record SkipDreamProposalCommand(string RunId, string ProposalId, string Reason);

public sealed class SkipDreamProposalHandler(
    IDreamRunRepository runs,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<DreamRun> HandleAsync(SkipDreamProposalCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.Reason)
            || command.Reason.Trim().Length < DreamProposal.RejectedReasonMinLength)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"reason must be at least {DreamProposal.RejectedReasonMinLength} characters.",
                new Dictionary<string, object?> { ["field"] = "reason" });
        }

        var runId = new DreamRunId(command.RunId);
        var proposalId = new DreamProposalId(command.ProposalId);
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync(
            inner => runs.SkipProposalAsync(runId, proposalId, command.Reason, now, inner),
            ct);

        return outcome switch
        {
            SkipDreamProposalOutcome.Skipped skipped => skipped.Run,
            SkipDreamProposalOutcome.RunNotFound => throw new ApiException(
                ErrorCodes.DreamRunNotFound,
                $"DreamRun '{command.RunId}' not found.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId }),
            SkipDreamProposalOutcome.ProposalNotFound => throw new ApiException(
                ErrorCodes.DreamProposalNotFound,
                $"Proposal '{command.ProposalId}' not found in DreamRun '{command.RunId}'.",
                new Dictionary<string, object?>
                {
                    ["run_id"] = command.RunId,
                    ["proposal_id"] = command.ProposalId,
                }),
            SkipDreamProposalOutcome.RunAlreadyClosed => throw new ApiException(
                ErrorCodes.DreamRunAlreadyClosed,
                $"DreamRun '{command.RunId}' is already closed.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId }),
            SkipDreamProposalOutcome.AlreadyDecided ad => throw new ApiException(
                ErrorCodes.DreamProposalAlreadyDecided,
                $"Proposal '{command.ProposalId}' has decision '{ad.CurrentDecision}'.",
                new Dictionary<string, object?>
                {
                    ["run_id"] = command.RunId,
                    ["proposal_id"] = command.ProposalId,
                    ["current_decision"] = ad.CurrentDecision,
                }),
            _ => throw new InvalidOperationException($"Unhandled skip outcome: {outcome.GetType().Name}"),
        };
    }
}
