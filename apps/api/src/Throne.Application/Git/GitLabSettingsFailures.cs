using Throne.Application.Errors;

namespace Throne.Application.Git;

internal static class GitLabSettingsFailures
{
    public static ApiException HostInvalid(string? host, string reason) =>
        new(
            ErrorCodes.GitLabHostInvalid,
            $"GitLab host '{host}' is invalid: {reason}",
            new Dictionary<string, object?>
            {
                ["host"] = host,
                ["reason"] = reason,
            });
}

/// <summary>
/// Normaliser shared by the wire-level setter and the Mongo provider's lazy seed.
/// Rejects empty / whitespace input and obvious scheme prefixes (the CLI wants a bare
/// hostname; a URL silently corrupts <c>GITLAB_HOST</c>).
/// </summary>
public static class GitLabHostNormalizer
{
    public const string Default = "gitlab.com";

    public static string Validate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw GitLabSettingsFailures.HostInvalid(input, "host must be non-empty");
        }
        var trimmed = input.Trim();
        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            throw GitLabSettingsFailures.HostInvalid(trimmed, "host must not include a URL scheme");
        }
        return trimmed;
    }
}
