using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

/// <summary>
/// Move an Intent to a position defined by neighbours: <paramref name="BeforeId"/>
/// is the intent the moved item should follow, <paramref name="AfterId"/> is the one
/// it should precede. At least one must be supplied. The repository reads the pivots'
/// sort keys and computes the midpoint — clients never send keys.
/// </summary>
public sealed record MoveIntentCommand(string IntentId, string? BeforeId, string? AfterId);

public sealed class MoveIntentHandler(
    IIntentOrderingRepository ordering,
    IUnitOfWork unitOfWork)
{
    public async Task<Intent> HandleAsync(MoveIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.BeforeId) && string.IsNullOrWhiteSpace(command.AfterId))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "At least one of before_id or after_id must be supplied.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                });
        }

        var id = new IntentId(command.IntentId);
        var beforeId = string.IsNullOrWhiteSpace(command.BeforeId) ? (IntentId?)null : new IntentId(command.BeforeId);
        var afterId = string.IsNullOrWhiteSpace(command.AfterId) ? (IntentId?)null : new IntentId(command.AfterId);

        MoveIntentOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteAsync(
                inner => ordering.MoveBetweenAsync(id, beforeId, afterId, inner),
                ct);
        }
        catch (ArgumentException ex) when (ex.ParamName is "before" or "beforeId")
        {
            // FractionalIndex.Between rejects an inverted pivot pair. Reaching this branch
            // means the client computed neighbours from a stale or differently-ordered
            // view of the list (e.g. JS localeCompare vs server ordinal). Surface as 422
            // so the client refetches instead of bubbling up as a 500.
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Pivots are out of order — refresh the list and retry the move.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["before_id"] = command.BeforeId,
                    ["after_id"] = command.AfterId,
                });
        }

        return outcome switch
        {
            MoveIntentOutcome.Moved moved => moved.Intent,
            MoveIntentOutcome.NotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),
            MoveIntentOutcome.PivotNotFound pivot => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Pivot intent '{pivot.PivotId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = pivot.PivotId }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
