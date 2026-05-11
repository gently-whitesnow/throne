using Throne.Application.Events;
using Throne.Domain.Dreams;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence result of <see cref="IDreamSessionRepository.CreateAsync"/>.
/// Always carries a <see cref="DreamSessionRecorded"/> domain event; the
/// dispatching unit-of-work decorator fans it out after a successful commit.
/// </summary>
public sealed record CreateDreamSessionOutcome(DreamSession Session) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => [new DreamSessionRecorded(Session)];
}
