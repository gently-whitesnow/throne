using System.Text.RegularExpressions;

namespace Throne.Domain.Repositories;

/// <summary>
/// Allow-list validation for <see cref="RepoCoordinate"/> components. The patterns
/// mirror provider-specific slug rules and additionally fence the GitHub workspace
/// path layout (ADR-0024 § 1 uses <c>owner__repo</c> as the directory separator).
/// </summary>
public static partial class RepoCoordinateGuards
{
    public const int MaxLength = 100;
    public const int GitLabOwnerMaxLength = 255;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,38}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepoPattern();

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex GitLabSlugPattern();

    public static void EnsureValidOwner(string provider, string owner)
    {
        if (provider == GitProviderNames.GitLab)
        {
            EnsureValidGitLabOwner(owner);
            return;
        }

        EnsureValidGitHubPart(
            value: owner,
            paramName: nameof(owner),
            kind: "owner",
            pattern: OwnerPattern(),
            patternHint: "letters, digits, hyphen; must not start with hyphen");
    }

    public static void EnsureValidRepo(string provider, string repo)
    {
        if (provider == GitProviderNames.GitLab)
        {
            EnsureValidGitLabSlug(repo, nameof(repo), "repo");
            return;
        }

        EnsureValidGitHubPart(
            value: repo,
            paramName: nameof(repo),
            kind: "repo",
            pattern: RepoPattern(),
            patternHint: "letters, digits, '.', '_', '-'");
        if (repo is "." or "..")
        {
            throw new ArgumentException("repo must not be '.' or '..'.", nameof(repo));
        }
    }

    private static void EnsureValidGitHubPart(
        string value,
        string paramName,
        string kind,
        Regex pattern,
        string patternHint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"{kind} exceeds {MaxLength} characters.", paramName);
        }
        if (value.Contains("__", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{kind} must not contain '__' (workspace layout separator).",
                paramName);
        }
        if (!pattern.IsMatch(value))
        {
            throw new ArgumentException(
                $"{kind} '{value}' is not a valid GitHub identifier ({patternHint}).",
                paramName);
        }
    }

    private static void EnsureValidGitLabOwner(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (owner.Length > GitLabOwnerMaxLength)
        {
            throw new ArgumentException($"{nameof(owner)} exceeds {GitLabOwnerMaxLength} characters.", nameof(owner));
        }

        var segments = owner.Split('/');
        if (segments.Length == 0 || segments.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("owner must be a non-empty GitLab namespace path.", nameof(owner));
        }

        foreach (var segment in segments)
        {
            EnsureValidGitLabSlug(segment, nameof(owner), "owner segment");
        }
    }

    private static void EnsureValidGitLabSlug(string value, string paramName, string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"{kind} exceeds {MaxLength} characters.", paramName);
        }
        if (value is "." or "..")
        {
            throw new ArgumentException($"{kind} must not be '.' or '..'.", paramName);
        }
        if (!GitLabSlugPattern().IsMatch(value))
        {
            throw new ArgumentException(
                $"{kind} '{value}' is not a valid GitLab slug "
                + "(letters, digits, '.', '_', '-'; must start and end with a letter or digit).",
                paramName);
        }
    }
}
