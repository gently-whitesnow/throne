namespace Throne.Domain.Intents.Training;

public sealed record IntentReview(
    string Id,
    IntentId IntentId,
    int IntentVersionAtWrite,
    string Note,
    string Reason,
    DateTimeOffset CreatedAt,
    IntentTrainingAuthor CreatedBy)
{
    public static IntentReview Create(
        string id,
        IntentId intentId,
        int intentVersionAtWrite,
        string note,
        string reason,
        DateTimeOffset now,
        IntentTrainingAuthor createdBy)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(reason);
        if (note.Length == 0)
        {
            throw new ArgumentException("note must not be empty.", nameof(note));
        }

        if (reason.Length == 0)
        {
            throw new ArgumentException("reason must not be empty.", nameof(reason));
        }

        if (intentVersionAtWrite < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(intentVersionAtWrite), "intent_version_at_write must be >= 1.");
        }

        return new IntentReview(id, intentId, intentVersionAtWrite, note, reason, now, createdBy);
    }
}
