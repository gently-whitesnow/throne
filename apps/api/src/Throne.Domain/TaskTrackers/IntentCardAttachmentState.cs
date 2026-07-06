namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Mutable portion of <see cref="IntentCardAttachment"/> grouped into a record so state transitions can
/// be expressed as <c>State = State with { … }</c> inside the aggregate's instance methods. Holds the
/// last-pulled <see cref="Snapshot"/>, the recorded <see cref="Availability"/>
/// (<see cref="CardAvailabilityNames"/>), and the metadata timestamp.
/// </summary>
public sealed record IntentCardAttachmentState(
    CardSnapshot Snapshot,
    string Availability,
    DateTimeOffset UpdatedAt);
