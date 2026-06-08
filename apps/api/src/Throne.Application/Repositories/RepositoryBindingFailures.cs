using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Centralised ApiException factory for <see cref="RepositoryBindingService"/>. Keeps the
/// service body focused on workflow and free of error-construction boilerplate.
/// </summary>
internal static class RepositoryBindingFailures
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

    public static ApiException ProviderUnsupported(string provider) =>
        new(
            ErrorCodes.RepositoryProviderUnsupported,
            $"Git provider '{provider}' is not supported on this Throne build.",
            new Dictionary<string, object?>
            {
                ["provider"] = provider,
                ["supported_providers"] = new[] { GitProviderNames.GitHub, GitProviderNames.GitLab },
            });

    public static ApiException ProviderCapabilityDisabled(string provider, string capability) =>
        new(
            ErrorCodes.CapabilityDisabled,
            $"Git provider '{provider}' requires capability '{capability}' — enable it in /settings before retrying.",
            new Dictionary<string, object?>
            {
                ["provider"] = provider,
                ["capability"] = capability,
            });

    public static ApiException ProviderNotAuthenticated(ProviderAuthStatus status) =>
        new(
            ErrorCodes.RepositoryProviderNotAuthenticated,
            status.Detail ?? $"Git provider '{status.Provider}' CLI is not authenticated.",
            new Dictionary<string, object?>
            {
                ["provider"] = status.Provider,
                ["host"] = status.Host,
                ["account"] = status.Account,
            });

    public static ApiException DuplicateBinding(IntentRepositoryBinding existing) =>
        new(
            ErrorCodes.RepositoryBindingAlreadyExists,
            $"Repository '{existing.Coordinate.FullName}' is already bound to intent '{existing.IntentId.Value}'. "
                + "Unbind first to switch the tracked pull request.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = existing.IntentId.Value,
                ["binding_id"] = existing.Id.Value,
                ["provider"] = existing.Coordinate.Provider,
                ["owner"] = existing.Coordinate.Owner,
                ["repo"] = existing.Coordinate.Repo,
            });

    public static ApiException BindingNotReady(IntentRepositoryBinding binding) =>
        new(
            ErrorCodes.RepositoryNotReady,
            $"Binding '{binding.Id.Value}' is in '{binding.State.CloneStatus}'; manual sync requires a ready clone.",
            new Dictionary<string, object?>
            {
                ["binding_id"] = binding.Id.Value,
                ["clone_status"] = binding.State.CloneStatus,
                ["clone_error"] = binding.State.CloneError,
            });

    public static ApiException PullRequestNotAttached(IntentRepositoryBinding binding) =>
        new(
            ErrorCodes.RepositoryPullRequestNotAttached,
            $"Binding '{binding.Id.Value}' has no pull request attached; nothing to sync.",
            new Dictionary<string, object?>
            {
                ["binding_id"] = binding.Id.Value,
            });

    public static ApiException UpstreamGone(IntentRepositoryBinding binding) =>
        new(
            ErrorCodes.RepositoryUpstreamGone,
            $"Upstream pull request for binding '{binding.Id.Value}' is no longer reachable (404). "
                + "Binding moved to 'broken'.",
            new Dictionary<string, object?>
            {
                ["binding_id"] = binding.Id.Value,
                ["clone_status"] = binding.State.CloneStatus,
            });

    public static ApiException InvalidCoordinate(string owner, string repo, string detail) =>
        new(
            ErrorCodes.RepositoryProviderUnsupported,
            detail,
            new Dictionary<string, object?>
            {
                ["owner"] = owner,
                ["repo"] = repo,
            });
}
