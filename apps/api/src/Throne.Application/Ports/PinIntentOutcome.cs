using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record PinIntentOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Pinned(Intent Intent, IntentPin Pin, bool Created) : PinIntentOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events =>
            [new IntentPinned(Pin.IntentId.Value, Pin.ContextTagId.Value, Pin.PinSortKey)];
    }

    public sealed record IntentNotFound : PinIntentOutcome;

    public sealed record ContextTagNotFound(string ContextTagId) : PinIntentOutcome;

    public sealed record PivotNotFound(string PivotId) : PinIntentOutcome;
}

public abstract record UnpinIntentOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Unpinned(Intent Intent, string ContextTagId, bool Removed) : UnpinIntentOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events =>
            Removed ? [new IntentUnpinned(Intent.Id.Value, ContextTagId)] : [];
    }

    public sealed record IntentNotFound : UnpinIntentOutcome;
}

public abstract record MovePinOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Moved(Intent Intent, IntentPin Pin, bool Changed) : MovePinOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => Changed
            ? [new IntentPinMoved(Pin.IntentId.Value, Pin.ContextTagId.Value, Pin.PinSortKey)]
            : [];
    }

    public sealed record IntentNotFound : MovePinOutcome;

    public sealed record PinNotFound : MovePinOutcome;

    public sealed record PivotNotFound(string PivotId) : MovePinOutcome;
}
