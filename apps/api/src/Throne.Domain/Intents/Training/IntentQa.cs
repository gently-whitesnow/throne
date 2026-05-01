namespace Throne.Domain.Intents.Training;

public sealed record IntentQa(
    string Id,
    IntentId IntentId,
    int IntentVersionAtWrite,
    string Question,
    string Answer,
    DateTimeOffset CreatedAt,
    IntentTrainingAuthor CreatedBy)
{
    public static IntentQa Create(
        string id,
        IntentId intentId,
        int intentVersionAtWrite,
        string question,
        string answer,
        DateTimeOffset now,
        IntentTrainingAuthor createdBy)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(answer);
        if (question.Length == 0)
        {
            throw new ArgumentException("question must not be empty.", nameof(question));
        }

        if (answer.Length == 0)
        {
            throw new ArgumentException("answer must not be empty.", nameof(answer));
        }

        if (intentVersionAtWrite < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(intentVersionAtWrite), "intent_version_at_write must be >= 1.");
        }

        return new IntentQa(id, intentId, intentVersionAtWrite, question, answer, now, createdBy);
    }
}
