using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record InsertIntentTextAfterLineOutcome : IDomainEventCarrier
{
    private InsertIntentTextAfterLineOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Inserted(Intent Intent) : InsertIntentTextAfterLineOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentTextChanged(Intent)];
    }

    public sealed record NotFound : InsertIntentTextAfterLineOutcome;

    public sealed record VersionConflict(int CurrentVersion) : InsertIntentTextAfterLineOutcome;

    public sealed record LineOutOfRange(int TotalLines, int RequestedAfterLine) : InsertIntentTextAfterLineOutcome;
}
