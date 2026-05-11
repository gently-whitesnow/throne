using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.Intents.Linking;

public sealed class LinkIntentHandler(
    IIntentLinkRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IntentLink> HandleAsync(LinkIntentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.FromId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ToId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Type);

        if (string.Equals(command.FromId, command.ToId, StringComparison.Ordinal))
        {
            throw new ApiException(
                LinkErrorCodes.SelfLink,
                "An intent cannot link to itself.",
                new Dictionary<string, object?>
                {
                    ["from_id"] = command.FromId,
                    ["to_id"] = command.ToId,
                });
        }

        if (!IntentLinkType.IsKnown(command.Type))
        {
            throw new ApiException(
                LinkErrorCodes.TypeUnsupported,
                $"Unknown link type '{command.Type}'.",
                new Dictionary<string, object?> { ["type"] = command.Type });
        }

        if (!IntentLinkType.IsSupportedStage1(command.Type))
        {
            throw new ApiException(
                LinkErrorCodes.TypeUnsupported,
                $"Link type '{command.Type}' is reserved for a later stage.",
                new Dictionary<string, object?> { ["type"] = command.Type });
        }

        var link = IntentLink.Create(
            id: Guid.NewGuid().ToString("N"),
            fromId: new IntentId(command.FromId),
            toId: new IntentId(command.ToId),
            type: command.Type,
            author: command.Author,
            rationale: command.Rationale,
            createdAt: clock.GetUtcNow());

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.CreateAsync(link, inner),
            ct);

        return outcome switch
        {
            CreateIntentLinkOutcome.Created created => created.Link,
            CreateIntentLinkOutcome.IntentNotFound notFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{notFound.MissingIntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = notFound.MissingIntentId }),
            CreateIntentLinkOutcome.Duplicate => throw new ApiException(
                LinkErrorCodes.Duplicate,
                $"A '{command.Type}' link from '{command.FromId}' to '{command.ToId}' already exists.",
                new Dictionary<string, object?>
                {
                    ["from_id"] = command.FromId,
                    ["to_id"] = command.ToId,
                    ["type"] = command.Type,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
