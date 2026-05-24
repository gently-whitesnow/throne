using System.Text.RegularExpressions;

namespace Throne.Domain.Repositories;

/// <summary>
/// Allow-list validation for <see cref="RepoCoordinate"/> components. The patterns
/// mirror GitHub's own allowed character sets and additionally fence the workspace
/// path layout (ADR-0024 § 1 uses <c>owner__repo</c> as the directory separator —
/// so neither field may contain the substring <c>__</c>).
///
/// Splitting the guards out keeps the value-type ctor body within the per-class
/// CA1502 budget and makes the rules testable in isolation.
/// </summary>
public static partial class RepoCoordinateGuards
{
    /// <summary>Max length per GitHub field; gives a sane wire upper bound.</summary>
    public const int MaxLength = 100;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9-]{0,38}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RepoPattern();

    public static void EnsureValidOwner(string owner) =>
        EnsureValid(
            value: owner,
            paramName: nameof(owner),
            kind: "owner",
            pattern: OwnerPattern(),
            patternHint: "letters, digits, hyphen; must not start with hyphen");

    public static void EnsureValidRepo(string repo)
    {
        EnsureValid(
            value: repo,
            paramName: nameof(repo),
            kind: "repo",
            pattern: RepoPattern(),
            patternHint: "letters, digits, '.', '_', '-'");
        // Extra fence: `.` / `..` pass the regex above but would still walk the
        // filesystem if reused as a path segment. ADR-0024 § 1 workspace layout
        // joins repo as `{owner}__{repo}` so the standalone names are unreachable
        // via the workspace path, but reject them anyway as a belt-and-braces
        // safeguard against future layout changes.
        if (repo is "." or "..")
        {
            throw new ArgumentException("repo must not be '.' or '..'.", nameof(repo));
        }
    }

    private static void EnsureValid(
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
}
