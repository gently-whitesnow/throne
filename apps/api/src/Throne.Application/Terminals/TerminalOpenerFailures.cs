using Throne.Application.Errors;

namespace Throne.Application.Terminals;

internal static class TerminalOpenerFailures
{
    public static ApiException IntentNotFound(string intentId) =>
        new(
            ErrorCodes.IntentNotFound,
            $"Intent '{intentId}' not found.",
            new Dictionary<string, object?> { ["intent_id"] = intentId });

    public static ApiException SessionNotLive(string intentId, string sessionName) =>
        new(
            TerminalErrorCodes.SessionNotLive,
            $"Native terminal requires a live tmux session '{sessionName}' for intent '{intentId}'.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["session_name"] = sessionName,
            });

    public static ApiException ProviderUnavailable(string provider, string? detail) =>
        new(
            TerminalErrorCodes.NativeProviderUnavailable,
            $"Selected native terminal provider '{provider}' is not detected on the host. {detail}".TrimEnd(),
            new Dictionary<string, object?>
            {
                ["reason"] = "selected_not_detected",
                ["provider"] = provider,
                ["detail"] = detail,
            });

    public static ApiException NoProviderAvailable() =>
        new(
            TerminalErrorCodes.NativeProviderUnavailable,
            "No native terminal provider is detected on the host.",
            new Dictionary<string, object?> { ["reason"] = "none_detected" });

    public static ApiException AmbiguousProvider(IReadOnlyList<string> detectedProviders) =>
        new(
            TerminalErrorCodes.NativeProviderUnavailable,
            $"Multiple native terminal providers are detected ({string.Join(", ", detectedProviders)}) but none is selected.",
            new Dictionary<string, object?>
            {
                ["reason"] = "ambiguous",
                ["detected_providers"] = detectedProviders,
            });
}
