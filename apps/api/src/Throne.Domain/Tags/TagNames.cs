using System.Text.RegularExpressions;

namespace Throne.Domain.Tags;

public static partial class TagNames
{
    public const int MaxLength = 40;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    /// <summary>
    /// Normalizes a raw tag name into the canonical slug form: trimmed, lowercased,
    /// leading hashes stripped, internal whitespace replaced with '-'. Returns the
    /// normalized value or throws when the result is empty / not a valid slug.
    /// </summary>
    public static string Normalize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var trimmed = raw.Trim().TrimStart('#').Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Tag name must not be empty.", nameof(raw));
        }

        var lowered = trimmed.ToLowerInvariant();
        var collapsed = WhitespaceRegex().Replace(lowered, "-");
        if (collapsed.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Tag name must not exceed {MaxLength} characters (got {collapsed.Length}).",
                nameof(raw));
        }

        if (!SlugRegex().IsMatch(collapsed))
        {
            throw new ArgumentException(
                $"Tag name '{collapsed}' is not a valid slug. Allowed: a-z, 0-9, '-', '_'; must start with letter or digit.",
                nameof(raw));
        }

        return collapsed;
    }

    /// <summary>True when the value is already the canonical normalized form.</summary>
    public static bool IsNormalized(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length is > 0 and <= MaxLength && SlugRegex().IsMatch(value);
    }

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
