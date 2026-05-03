using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

public sealed record CloseDreamRunCommand(string RunId, bool? ReleaseEvidence);

public sealed class CloseDreamRunHandler(
    IDreamRunRepository runs,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<DreamRun> HandleAsync(CloseDreamRunCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        var runId = new DreamRunId(command.RunId);
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync(
            inner => runs.CloseAsync(runId, command.ReleaseEvidence, now, inner),
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
