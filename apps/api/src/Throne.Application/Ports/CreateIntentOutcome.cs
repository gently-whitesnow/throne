using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Ports;

/// <summary>
/// Outcome wrapper for a successful intent creation. Always carries
/// <see cref="IntentCreated"/>; failure paths throw via the repository (creation has
/// no domain-level branching today).
/// </summary>
public sealed record CreateIntentOutcome(Intent Intent) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => [new IntentCreated(Intent)];
}
