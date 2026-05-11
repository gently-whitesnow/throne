using Throne.Application.Events;
using Throne.Application.Intents;

namespace Throne.Application.Ports;

public sealed record UploadIntentAttachmentOutcome(IntentAttachment Attachment) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => [new IntentAttachmentAdded(Attachment)];
}
