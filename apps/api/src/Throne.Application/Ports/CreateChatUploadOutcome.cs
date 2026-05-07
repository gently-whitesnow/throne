using Throne.Application.ChatUploads;
using Throne.Application.Events;

namespace Throne.Application.Ports;

public sealed record CreateChatUploadOutcome(ChatUpload Upload) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => [new ChatUploadCreated(Upload)];
}
