using Throne.Application.Events;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public abstract record CreateTagOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Created(Tag Tag) : CreateTagOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new TagCreated(Tag)];
    }

    public sealed record NameTaken(Tag Existing) : CreateTagOutcome;
}
