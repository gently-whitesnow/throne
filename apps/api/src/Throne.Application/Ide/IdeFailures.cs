using Throne.Application.Errors;

namespace Throne.Application.Ide;

internal static class IdeFailures
{
    public static ApiException IntentNotFound(string intentId) =>
        new(
            ErrorCodes.IntentNotFound,
            $"Intent '{intentId}' not found.",
            new Dictionary<string, object?> { ["intent_id"] = intentId });

    public static ApiException BindingNotFound(string intentId, string bindingId) =>
        new(
            ErrorCodes.RepositoryBindingNotFound,
            $"Repository binding '{bindingId}' not found for intent '{intentId}'.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["binding_id"] = bindingId,
            });

    public static ApiException WorkspaceNotReady(string bindingId, string cloneStatus) =>
        new(
            ErrorCodes.RepositoryNotReady,
            $"Binding '{bindingId}' is not ready (clone_status={cloneStatus}). Wait for the clone before opening it.",
            new Dictionary<string, object?>
            {
                ["binding_id"] = bindingId,
                ["clone_status"] = cloneStatus,
            });

    public static ApiException ProviderUnavailable(string provider, string? detail) =>
        new(
            ErrorCodes.IdeProviderUnavailable,
            $"Selected IDE provider '{provider}' is not detected on the host. {detail}".TrimEnd(),
            new Dictionary<string, object?>
            {
                ["reason"] = "selected_not_detected",
                ["provider"] = provider,
                ["detail"] = detail,
            });

    public static ApiException NoProviderAvailable() =>
        new(
            ErrorCodes.IdeProviderUnavailable,
            "No IDE provider is detected on the host — install VS Code (`code`) or Cursor (`cursor`) before retrying.",
            new Dictionary<string, object?>
            {
                ["reason"] = "none_detected",
            });

    public static ApiException AmbiguousProvider(IReadOnlyList<string> detectedProviders) =>
        new(
            ErrorCodes.IdeProviderUnavailable,
            $"Multiple IDE providers are detected ({string.Join(", ", detectedProviders)}) but none is selected — choose one in /settings before retrying.",
            new Dictionary<string, object?>
            {
                ["reason"] = "ambiguous",
                ["detected_providers"] = detectedProviders,
            });
}
