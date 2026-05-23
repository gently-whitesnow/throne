namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Pulls the <c>X-OAuth-Scopes</c> header out of a <c>gh api user -i</c> response
/// and projects it both as a structured array (for the settings DTO) and as a
/// human-readable detail string (for the legacy <c>Detail</c> field on
/// <c>ProviderAuthStatus</c>). Extracted from <see cref="GhAuthStatusParser"/>
/// so the parser body stays under the CA1502 cyclomatic budget.
/// </summary>
internal static class GhScopeReader
{
    public static GhScopeRead Read(Dictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (!headers.TryGetValue("X-OAuth-Scopes", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return new GhScopeRead(Array.Empty<string>(), null);
        }

        var values = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new GhScopeRead(values, $"scopes: {raw}");
    }
}

internal sealed record GhScopeRead(string[] Values, string? Detail);
