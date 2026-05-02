using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Application.Intents;

public sealed record CreateIntentCommand(
    string Text,
    IReadOnlyList<string>? Tags,
    TextVersionAuthor Author);

public sealed class CreateIntentHandler(
    IIntentRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(CreateIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.GetUtcNow();
        var id = IntentId.New();
        var intent = Intent.Create(id, command.Text, command.Tags, now);
        var initialVersion = TextVersion.CreateSnapshot(
            id: Guid.NewGuid().ToString("N"),
            ownerKind: TextVersionOwnerKind.Intent,
            ownerId: id.Value,
            snapshot: intent.Text,
            changedAt: now,
            changedBy: command.Author);
        var initialStatusChange = IntentStatusChange.Create(
            id: Guid.NewGuid().ToString("N"),
            intentId: id,
            intentVersionAtWrite: intent.CurrentVersion,
            fromStatus: intent.Status,
            toStatus: intent.Status,
            source: "create_intent",
            createdAt: now,
            createdBy: ToTrainingAuthor(command.Author));

        await unitOfWork.ExecuteAsync(
            inner => repository.CreateAsync(intent, initialVersion, initialStatusChange, inner),
            ct).ConfigureAwait(false);
        return intent;
    }

    private static IntentTrainingAuthor ToTrainingAuthor(TextVersionAuthor author) => author switch
    {
        TextVersionAuthor.User => IntentTrainingAuthor.User,
        TextVersionAuthor.Agent => IntentTrainingAuthor.Agent,
        TextVersionAuthor.System => IntentTrainingAuthor.System,
        _ => throw new InvalidOperationException($"Unknown author: {author}."),
    };
}
