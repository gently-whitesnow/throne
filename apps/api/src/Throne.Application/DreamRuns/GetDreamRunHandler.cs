using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;
using Throne.Domain.Instructions;

namespace Throne.Application.DreamRuns;

public sealed record GetDreamRunQuery(string RunId);

/// <summary>
/// Returns a DreamRun with a per-proposal preview diff: current instruction text +
/// proposed text after rule injection (the same transformation <c>apply</c> performs).
/// </summary>
public sealed class GetDreamRunHandler(
    IDreamRunRepository runs,
    IInstructionRepository instructions)
{
    public async Task<GetDreamRunResult> HandleAsync(GetDreamRunQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var run = await runs.GetByIdAsync(new DreamRunId(query.RunId), ct)
            ?? throw new ApiException(
                ErrorCodes.DreamRunNotFound,
                $"DreamRun '{query.RunId}' not found.",
                new Dictionary<string, object?> { ["run_id"] = query.RunId });

        var previews = new List<DreamProposalPreview>(run.Proposals.Count);
        foreach (var proposal in run.Proposals)
        {
            var instruction = await instructions.GetByIdAsync(new InstructionId(proposal.TargetInstructionId), ct);
            var currentText = instruction?.Text ?? string.Empty;
            var currentVersion = instruction?.CurrentVersion ?? 0;
            var proposedText = LearnedRulesInjector.Inject(currentText, proposal.FinalRule ?? proposal.ProposedRule);
            previews.Add(new DreamProposalPreview(
                proposal.Id.Value,
                currentText,
                proposedText,
                currentVersion,
                instruction is not null && instruction.CurrentVersion == proposal.BaseInstructionVersion));
        }

        return new GetDreamRunResult(run, previews);
    }
}

public sealed record GetDreamRunResult(DreamRun Run, IReadOnlyList<DreamProposalPreview> Previews);

public sealed record DreamProposalPreview(
    string ProposalId,
    string CurrentText,
    string ProposedText,
    int CurrentInstructionVersion,
    bool BaseVersionMatchesCurrent);
