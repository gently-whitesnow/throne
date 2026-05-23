namespace Throne.Application.Git;

/// <summary>
/// Result of <see cref="IGitProvider.GetAuthStatusAsync"/>. Surfaces whether the
/// vendor CLI (e.g. <c>gh auth status</c>) is installed and authenticated. The
/// settings page (T-16) renders this as a red/green indicator so the user knows
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
    string? Detail = null);
