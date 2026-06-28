using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Application.Intents;

public sealed record CreateIntentCommand(
    string Text,
    IReadOnlyList<string>? TagNames,
    TextVersionAuthor Author,
    string? Title = null);

public sealed class CreateIntentHandler(
    IIntentRepository repository,
    IIntentOrderingRepository ordering,
    IntentTagResolver tagResolver,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(CreateIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.GetUtcNow();
        var id = IntentId.New();

        var outcome = await unitOfWork.ExecuteAsync(
            async inner =>
            {
                var resolved = await tagResolver.EnsureByNamesAsync(command.TagNames, now, inner);
                var sortKey = await NextTopSortKeyAsync(inner);
                var intent = Intent.Create(id, command.Text, resolved.TagIds, now, sortKey: sortKey, title: command.Title);
                var initialVersion = TextVersion.CreateSnapshot(
                    id: Guid.NewGuid().ToString("N"),
                    ownerKind: TextVersionOwnerKind.Intent,
                    ownerId: id.Value,
                    snapshot: intent.State.Text,
                    changedAt: now,
                    changedBy: command.Author);
                var initialStatusChange = IntentStatusChange.Create(
                    id: Guid.NewGuid().ToString("N"),
                    intentId: id,
                    intentVersionAtWrite: intent.State.CurrentVersion,
                    fromStatus: intent.State.Status,
                    toStatus: intent.State.Status,
                    source: "create_intent",
                    createdAt: now,
                    createdBy: TextVersionAuthorMapping.ToTrainingAuthor(command.Author));

                return await repository.CreateAsync(
                    intent, initialVersion, initialStatusChange, resolved.CreatedTags, inner);
            },
            ct);

        return outcome.Intent;
    }

    private async Task<string> NextTopSortKeyAsync(CancellationToken ct)
    {
        var currentMin = await ordering.GetMinSortKeyAsync(ct);
        return FractionalIndex.Between(
            before: null,
            after: string.IsNullOrEmpty(currentMin) ? null : currentMin);
    }
}
