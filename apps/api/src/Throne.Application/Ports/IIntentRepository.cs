using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Application.Ports;

public interface IIntentRepository
{
    Task CreateAsync(Intent intent, TextVersion initialVersion, CancellationToken ct);

    Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct);

    Task<ReplaceIntentTextOutcome> ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
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
}
