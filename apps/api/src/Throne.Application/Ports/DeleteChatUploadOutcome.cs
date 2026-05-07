using Throne.Application.Events;

namespace Throne.Application.Ports;

public abstract record DeleteChatUploadOutcome : IDomainEventCarrier
{
    public abstract IReadOnlyList<IDomainEvent> Events { get; }

    public sealed record Deleted(string ChatUploadId) : DeleteChatUploadOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new ChatUploadDeleted(ChatUploadId)];
    }

    public sealed record NotFound : DeleteChatUploadOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Array.Empty<IDomainEvent>();
    }
}
