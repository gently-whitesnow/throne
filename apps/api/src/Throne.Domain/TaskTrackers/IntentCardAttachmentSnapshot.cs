using Throne.Domain.Intents;

namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Persistence-shaped snapshot used by <see cref="IntentCardAttachment.Restore"/>. Wire DTO that mirrors
/// the persistence row; the aggregate re-validates the availability enum on rehydration so a tampered row
/// fails fast.
/// </summary>
public sealed record IntentCardAttachmentSnapshot(
    CardAttachmentId Id,
    IntentId IntentId,
    CardCoordinate Coordinate,
    CardSnapshot Snapshot,
    string Availability,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
