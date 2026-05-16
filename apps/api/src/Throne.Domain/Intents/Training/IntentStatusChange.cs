namespace Throne.Domain.Intents.Training;

public sealed record IntentStatusChange(
    string Id,
    IntentId IntentId,
    int IntentVersionAtWrite,
    string FromStatus,
    string ToStatus,
    string Source,
    IntentStatusChangeAudit Audit,
    string? Reason)
{
    public static IntentStatusChange Create(
        string id,
        IntentId intentId,
        int intentVersionAtWrite,
        string fromStatus,
        string toStatus,
        string source,
        DateTimeOffset createdAt,
        IntentTrainingAuthor createdBy,
        string? reason = null)
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

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        return new IntentStatusChange(
            id,
            intentId,
            intentVersionAtWrite,
            fromStatus,
            toStatus,
            source,
            new IntentStatusChangeAudit(createdAt, createdBy),
            normalizedReason);
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

public sealed record IntentStatusChangeAudit(DateTimeOffset CreatedAt, IntentTrainingAuthor CreatedBy);
