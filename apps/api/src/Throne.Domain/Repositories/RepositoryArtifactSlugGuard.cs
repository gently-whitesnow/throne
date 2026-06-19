using System.Text.RegularExpressions;

namespace Throne.Domain.Repositories;

/// <summary>
/// Allow-list validation for <see cref="RepositoryArtifact.Slug"/>: a stable, URL-safe
/// kebab-case identifier for a knowledge page within one repository. Lower-case so
/// <c>(coordinate, slug)</c> uniqueness is not defeated by case variants.
/// </summary>
public static partial class RepositoryArtifactSlugGuard
{
    public const int MaxLength = 100;

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    public static void EnsureValid(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (slug.Length > MaxLength)
        {
            throw new ArgumentException($"slug exceeds {MaxLength} characters.", nameof(slug));
        }
        if (!SlugPattern().IsMatch(slug))
        {
            throw new ArgumentException(
                $"slug '{slug}' is not a valid kebab-case identifier (lower-case letters, digits, single hyphens).",
                nameof(slug));
        }
    }
}
