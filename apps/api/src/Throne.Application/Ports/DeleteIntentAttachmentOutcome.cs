using Throne.Application.Events;

namespace Throne.Application.Ports;

public abstract record DeleteIntentAttachmentOutcome : IDomainEventCarrier
{
    private DeleteIntentAttachmentOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Deleted(string IntentId, string AttachmentId) : DeleteIntentAttachmentOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events =>
            [new IntentAttachmentDeleted(IntentId, AttachmentId)];
    }

    public sealed record NotFound : DeleteIntentAttachmentOutcome;
}
