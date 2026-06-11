namespace Throne.Domain.Intents;

public sealed record IntentState(
    string Text,
    string Status,
    int CurrentVersion,
    string SortKey,
    DateTimeOffset UpdatedAt,
    bool CleanupLocalStateOnDone = true);
