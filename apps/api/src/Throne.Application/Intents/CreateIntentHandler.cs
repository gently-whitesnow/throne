using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;

namespace Throne.Application.Intents;

public sealed record CreateIntentCommand(
    string Text,
    IReadOnlyList<string>? TagNames,
    TextVersionAuthor Author);

public sealed class CreateIntentHandler(
    IIntentRepository repository,
    ITagRepository tagRepository,
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
                var (tagIds, createdTags) = await ResolveTagIdsAsync(command.TagNames, now, inner);

                var intent = Intent.Create(id, command.Text, tagIds, now);
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

                return await repository
                    .CreateAsync(intent, initialVersion, initialStatusChange, createdTags, inner)
                    ;
            },
            ct);

        return outcome.Intent;
    }

    private async Task<(IReadOnlyList<TagId> TagIds, IReadOnlyList<Tag> CreatedTags)> ResolveTagIdsAsync(
        IReadOnlyList<string>? rawNames,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (rawNames is null || rawNames.Count == 0)
        {
            return ([], []);
        }

        var tagIds = new List<TagId>(rawNames.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var created = new List<Tag>();

        foreach (var raw in rawNames)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var normalized = TagNames.Normalize(raw);
            if (!seen.Add(normalized))
            {
                continue;
            }

            var ensure = await tagRepository.EnsureByNameAsync(normalized, now, ct);
            tagIds.Add(ensure.Tag.Id);
            if (ensure is EnsureTagOutcome.Created createdTag)
            {
                created.Add(createdTag.Value);
            }
        }

        return (tagIds, created);
    }

    private static IntentTrainingAuthor ToTrainingAuthor(TextVersionAuthor author) => author switch
    {
        TextVersionAuthor.User => IntentTrainingAuthor.User,
        TextVersionAuthor.Agent => IntentTrainingAuthor.Agent,
        TextVersionAuthor.System => IntentTrainingAuthor.System,
        _ => throw new InvalidOperationException($"Unknown author: {author}."),
    };
}
