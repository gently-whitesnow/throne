using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Intents;

public sealed record PinIntentCommand(string IntentId, string ContextTagId, string? BeforeId, string? AfterId);

public sealed record UnpinIntentCommand(string IntentId, string ContextTagId);

public sealed record MovePinCommand(string IntentId, string ContextTagId, string? BeforeId, string? AfterId);

public sealed class PinIntentHandler(
    IIntentPinRepository pins,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(PinIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ContextTagId);

        var intentId = new IntentId(command.IntentId);
        var contextTagId = new TagId(command.ContextTagId);
        var beforeId = string.IsNullOrWhiteSpace(command.BeforeId) ? (IntentId?)null : new IntentId(command.BeforeId);
        var afterId = string.IsNullOrWhiteSpace(command.AfterId) ? (IntentId?)null : new IntentId(command.AfterId);
        var now = clock.GetUtcNow();

        PinIntentOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteAsync(
                inner => pins.PinAsync(intentId, contextTagId, beforeId, afterId, now, inner),
                ct);
        }
        catch (ArgumentException ex) when (ex.ParamName is "before" or "beforeId")
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Pin pivots are out of order — refresh the list and retry.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["context_tag_id"] = command.ContextTagId,
                });
        }

        return outcome switch
        {
            PinIntentOutcome.Pinned pinned => pinned.Intent,
            PinIntentOutcome.IntentNotFound => throw NotFound("Intent", command.IntentId, command),
            PinIntentOutcome.ContextTagNotFound ctx => throw NotFound("Tag", ctx.ContextTagId, command),
            PinIntentOutcome.PivotNotFound pivot => throw NotFound("Pivot pin", pivot.PivotId, command),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }

    private static ApiException NotFound(string kind, string id, PinIntentCommand command) =>
        new(
            ErrorCodes.IntentNotFound,
            $"{kind} '{id}' not found.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = command.IntentId,
                ["context_tag_id"] = command.ContextTagId,
            });
}

public sealed class UnpinIntentHandler(IIntentPinRepository pins, IUnitOfWork unitOfWork)
{
    public async Task<Intent> HandleAsync(UnpinIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ContextTagId);

        var intentId = new IntentId(command.IntentId);
        var contextTagId = new TagId(command.ContextTagId);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => pins.UnpinAsync(intentId, contextTagId, inner),
            ct);

        return outcome switch
        {
            UnpinIntentOutcome.Unpinned unpinned => unpinned.Intent,
            UnpinIntentOutcome.IntentNotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["context_tag_id"] = command.ContextTagId,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}

public sealed class MovePinHandler(IIntentPinRepository pins, IUnitOfWork unitOfWork)
{
    public async Task<Intent> HandleAsync(MovePinCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ContextTagId);

        if (string.IsNullOrWhiteSpace(command.BeforeId) && string.IsNullOrWhiteSpace(command.AfterId))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "At least one of before_id or after_id must be supplied.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["context_tag_id"] = command.ContextTagId,
                });
        }

        var intentId = new IntentId(command.IntentId);
        var contextTagId = new TagId(command.ContextTagId);
        var beforeId = string.IsNullOrWhiteSpace(command.BeforeId) ? (IntentId?)null : new IntentId(command.BeforeId);
        var afterId = string.IsNullOrWhiteSpace(command.AfterId) ? (IntentId?)null : new IntentId(command.AfterId);

        MovePinOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteAsync(
                inner => pins.MoveAsync(intentId, contextTagId, beforeId, afterId, inner),
                ct);
        }
        catch (ArgumentException ex) when (ex.ParamName is "before" or "beforeId")
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Pin pivots are out of order — refresh the list and retry.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["context_tag_id"] = command.ContextTagId,
                });
        }

        return outcome switch
        {
            MovePinOutcome.Moved moved => moved.Intent,
            MovePinOutcome.IntentNotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),
            MovePinOutcome.PinNotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Pin for intent '{command.IntentId}' in context '{command.ContextTagId}' not found.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["context_tag_id"] = command.ContextTagId,
                }),
            MovePinOutcome.PivotNotFound pivot => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Pivot pin '{pivot.PivotId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = pivot.PivotId }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
