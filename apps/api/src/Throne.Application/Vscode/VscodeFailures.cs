using Throne.Application.Errors;

namespace Throne.Application.Vscode;

internal static class VscodeFailures
{
    public static ApiException CapabilityDisabled() =>
        new(
            ErrorCodes.CapabilityDisabled,
            "Capability 'vscode' is disabled — enable it in /settings before retrying.",
            new Dictionary<string, object?> { ["capability"] = "vscode" });

    public static ApiException CapabilityUndetected() =>
        new(
            ErrorCodes.CapabilityDisabled,
            "The 'code' CLI is not available on the host — install it (Shell Command: Install 'code' command) before retrying.",
            new Dictionary<string, object?> { ["capability"] = "vscode" });

    public static ApiException SpawnFailed(string workspacePath, string? detail) =>
        new(
            ErrorCodes.TerminalSpawnFailed,
            detail ?? $"The 'code' CLI refused to open '{workspacePath}'.",
            new Dictionary<string, object?> { ["workspace_path"] = workspacePath });

    public static ApiException BindingCloneNotReady(string bindingId, string cloneStatus) =>
        new(
            ErrorCodes.RepositoryNotReady,
            $"Binding '{bindingId}' is not ready (clone_status={cloneStatus}). Wait for the clone before opening it.",
            new Dictionary<string, object?>
            {
                ["binding_id"] = bindingId,
                ["clone_status"] = cloneStatus,
            });
}
