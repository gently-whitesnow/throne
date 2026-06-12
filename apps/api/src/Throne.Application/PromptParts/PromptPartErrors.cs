using Throne.Application.Errors;

namespace Throne.Application.PromptParts;

internal static class PromptPartErrors
{
    public static ApiException Validation(ArgumentException ex) => new(
        ErrorCodes.ValidationFailed,
        ex.Message,
        new Dictionary<string, object?> { ["field"] = ex.ParamName });

    public static ApiException NotFound(string id) => new(
        ErrorCodes.PromptPartNotFound,
        $"Prompt part '{id}' not found.",
        new Dictionary<string, object?> { ["prompt_part_id"] = id });

    public static ApiException HasRoles(string id) => new(
        ErrorCodes.PromptPartHasRoles,
        $"Prompt part '{id}' still has roles in one or more modes. Detach all roles before deleting.",
        new Dictionary<string, object?> { ["prompt_part_id"] = id });
}
