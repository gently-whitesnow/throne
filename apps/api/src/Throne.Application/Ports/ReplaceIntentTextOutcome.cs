using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record ReplaceIntentTextOutcome : IDomainEventCarrier
{
    private ReplaceIntentTextOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Replaced(Intent Intent) : ReplaceIntentTextOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new IntentTextChanged(Intent)];
    }

    public sealed record NotFound : ReplaceIntentTextOutcome;

    public sealed record VersionConflict(int CurrentVersion) : ReplaceIntentTextOutcome;

    public sealed record MatchNotFound(string QueryPreview) : ReplaceIntentTextOutcome;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplaceIntentTextOutcome;
}
