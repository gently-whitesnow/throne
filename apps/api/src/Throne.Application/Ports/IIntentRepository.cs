using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;

namespace Throne.Application.Ports;

public interface IIntentRepository
{
    Task<CreateIntentOutcome> CreateAsync(
        Intent intent,
        TextVersion initialVersion,
        IntentStatusChange initialStatusChange,
        IReadOnlyList<Tag> upsertedTags,
        CancellationToken ct);

    Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct);

    Task<ReplaceIntentTextOutcome> ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct);

    Task<InsertIntentTextAfterLineOutcome> InsertTextAfterLineAsync(
        IntentId id,
        int expectedVersion,
        int afterLine,
        string insertText,
        DateTimeOffset now,
        CancellationToken ct);

    Task<IReadOnlyList<Intent>> ListAsync(CancellationToken ct);

    Task<DeleteIntentOutcome> DeleteAsync(IntentId id, CancellationToken ct);

    Task<SetIntentStatusOutcome> SetStatusAsync(
        IntentId id,
        string status,
        string? appendText,
        IntentTrainingAuthor changedBy,
        string source,
        DateTimeOffset now,
        CancellationToken ct);

    Task<SetIntentTagsOutcome> SetTagsAsync(
        IntentId id,
        int expectedVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset now,
        CancellationToken ct);
}
