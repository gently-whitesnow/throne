using Throne.Application.Errors;

namespace Throne.Application.Terminals;

internal static class RunPreflightFailures
{
    public static ApiException IntentNotFound(string intentId) =>
        new(
            ErrorCodes.IntentNotFound,
            $"Intent '{intentId}' not found.",
            new Dictionary<string, object?> { ["intent_id"] = intentId });
}
