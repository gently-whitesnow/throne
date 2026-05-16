namespace Throne.Domain.Intents;

public static class IntentStatusOperation
{
    public static bool SetStatus(Intent intent, string status, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(intent);
        IntentGuards.EnsureValidStatus(status, nameof(status));
        if (string.Equals(intent.State.Status, status, StringComparison.Ordinal))
        {
            return false;
        }

        intent.State = intent.State with { Status = status, UpdatedAt = now };
        return true;
    }
}
