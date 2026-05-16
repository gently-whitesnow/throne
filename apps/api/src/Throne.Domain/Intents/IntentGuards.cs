namespace Throne.Domain.Intents;

internal static class IntentGuards
{
    public static void EnsureValidStatus(string status, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!IntentStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(paramName, $"Unknown intent status: {status}.");
        }
    }

    public static void EnsureValidCurrentVersion(int version)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "current_version must be >= 1.");
        }
    }

    public static void EnsureCreateInputs(string ownerUserId, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            throw new ArgumentException("Intent text must not be empty.", nameof(text));
        }
    }
}
