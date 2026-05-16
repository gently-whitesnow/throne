namespace Throne.Domain.Tags;

internal static class TagGuards
{
    public static void EnsureValidRestore(string name, int currentVersion)
    {
        if (!TagNames.IsNormalized(name))
        {
            throw new ArgumentException($"Stored tag name '{name}' is not normalized.", nameof(name));
        }

        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "current_version must be >= 1.");
        }
    }
}
