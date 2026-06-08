namespace Throne.Application.Git;

/// <summary>
/// Result of <see cref="IGitProvider.GetAuthStatusAsync"/>. Surfaces whether the
/// vendor CLI (e.g. <c>gh auth status</c>) is installed and authenticated. The
/// settings page renders this as a red/green indicator so the user knows
/// they need to run <c>gh auth login</c>.
/// </summary>
/// <param name="Provider">Provider wire-format name (e.g. <c>github</c>).</param>
/// <param name="IsAuthenticated">
///   <see langword="true"/> when the CLI reports an authenticated session.
/// </param>
/// <param name="Account">Authenticated account login, when available.</param>
/// <param name="Host">Configured host (e.g. <c>github.com</c>).</param>
/// <param name="Detail">
///   Human-readable diagnostic line when not authenticated — surfaced verbatim
///   in the UI so the user can act on it without re-running the CLI.
/// </param>
public sealed record ProviderAuthStatus(
    string Provider,
    bool IsAuthenticated,
    string? Account = null,
    string? Host = null,
    string? Detail = null,
    string State = ProviderAuthStateNames.Unauthenticated)
{
    /// <summary>
    /// OAuth scopes observed for the active session (parsed from the upstream
    /// <c>X-OAuth-Scopes</c> response header). Empty when the CLI did not surface
    /// any scope information. Surfaced as the <c>scopes</c> array in the
    /// <c>GitProviderAuthStatusDto</c> contract DTO.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();
}
