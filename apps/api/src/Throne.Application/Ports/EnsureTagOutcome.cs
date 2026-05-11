using Throne.Application.Events;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public abstract record EnsureTagOutcome : IDomainEventCarrier
{
    public abstract Tag Tag { get; }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Created(Tag Value) : EnsureTagOutcome
    {
        public override Tag Tag => Value;
        public override IReadOnlyList<IDomainEvent> Events => [new TagCreated(Value)];
    }

    public sealed record Existed(Tag Value) : EnsureTagOutcome
    {
        public override Tag Tag => Value;
    }
}
