namespace Throne.Domain.Intents.Training;

public sealed record IntentStatusChange(
    string Id,
    IntentId IntentId,
    int IntentVersionAtWrite,
    string FromStatus,
    string ToStatus,
    string Source,
    DateTimeOffset CreatedAt,
    IntentTrainingAuthor CreatedBy)
{
    public static IntentStatusChange Create(
        string id,
        IntentId intentId,
        int intentVersionAtWrite,
        string fromStatus,
        string toStatus,
        string source,
        DateTimeOffset createdAt,
        IntentTrainingAuthor createdBy)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(source);
        ValidateStatus(fromStatus, nameof(fromStatus));
        ValidateStatus(toStatus, nameof(toStatus));

        if (intentVersionAtWrite < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intentVersionAtWrite),
                "intent_version_at_write must be >= 1.");
        }

        return new IntentStatusChange(
            id,
            intentId,
            intentVersionAtWrite,
            fromStatus,
            toStatus,
            source,
            createdAt,
            createdBy);
    }

    private static void ValidateStatus(string status, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        if (!IntentStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(paramName, $"Unknown intent status: {status}.");
        }
    }
}
