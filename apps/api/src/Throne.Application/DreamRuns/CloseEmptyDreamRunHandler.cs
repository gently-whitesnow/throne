using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

public sealed record CloseEmptyDreamRunCommand(string RunId, bool? ReleaseEvidence);

/// <summary>
/// Closes a still-pending DreamRun that the agent considers «not actionable» — no
/// proposals were generated. Forced closes of runs with proposals are explicitly
/// out of scope for the MCP surface; those are user-actions over HTTP only
/// (Intent 4 §close_empty_dream_run).
/// </summary>
public sealed class CloseEmptyDreamRunHandler(
    IDreamRunRepository runs,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<DreamRun> HandleAsync(CloseEmptyDreamRunCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.RunId))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "run_id must be a non-empty string.",
                new Dictionary<string, object?> { ["field"] = "run_id" });
        }

        var runId = new DreamRunId(command.RunId);
        var existing = await runs.GetByIdAsync(runId, ct)
            ?? throw new ApiException(
                ErrorCodes.DreamRunNotFound,
                $"DreamRun '{command.RunId}' not found.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId });

        if (existing.IsClosed)
        {
            throw new ApiException(
                ErrorCodes.DreamRunAlreadyClosed,
                $"DreamRun '{command.RunId}' is already closed.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId });
        }
        if (existing.Proposals.Count > 0)
        {
            throw new ApiException(
                ErrorCodes.DreamRunHasProposals,
                $"DreamRun '{command.RunId}' has {existing.Proposals.Count} proposals; agents cannot close runs with proposals — that is a user-only action.",
                new Dictionary<string, object?>
                {
                    ["run_id"] = command.RunId,
                    ["proposals_count"] = existing.Proposals.Count,
                });
        }

        var releaseEvidence = command.ReleaseEvidence ?? true;
        var now = clock.GetUtcNow();
        var outcome = await unitOfWork.ExecuteAsync(
            inner => runs.CloseAsync(runId, releaseEvidence, now, inner),
            ct);

        return outcome switch
        {
            CloseDreamRunOutcome.Closed closed => closed.Run,
            CloseDreamRunOutcome.NotFound => throw new ApiException(
                ErrorCodes.DreamRunNotFound,
                $"DreamRun '{command.RunId}' not found.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId }),
            CloseDreamRunOutcome.AlreadyClosed => throw new ApiException(
                ErrorCodes.DreamRunAlreadyClosed,
                $"DreamRun '{command.RunId}' is already closed.",
                new Dictionary<string, object?> { ["run_id"] = command.RunId }),
            _ => throw new InvalidOperationException($"Unhandled close outcome: {outcome.GetType().Name}"),
        };
    }
}
