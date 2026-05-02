using Throne.Application.Events;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Ports;

public abstract record DeleteTagOutcome : IDomainEventCarrier
{
    public virtual IReadOnlyList<IDomainEvent> Events => [];

    public sealed record Deleted(TagId TagId, IReadOnlyList<Intent> AffectedIntents) : DeleteTagOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events
        {
            get
            {
                var events = new List<IDomainEvent>(AffectedIntents.Count + 1);
                foreach (var intent in AffectedIntents)
                {
                    events.Add(new IntentTagsChanged(intent));
                }
                events.Add(new TagDeleted(TagId.Value));
                return events;
            }
        }
    }

    public sealed record NotFound : DeleteTagOutcome;
}
