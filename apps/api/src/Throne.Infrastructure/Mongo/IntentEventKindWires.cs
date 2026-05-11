namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Wire-format constants for <c>intent_events.kind</c>. Mirrored in domain
/// (<see cref="Throne.Domain.Intents.Events.IntentEventKindExtensions"/>); declared
/// here only because Mongo index <c>PartialFilterExpression</c> is built from a raw
/// <c>BsonDocument</c> outside the domain assembly.
/// </summary>
internal static class IntentEventKindWires
{
    public const string TextChanged = "text_changed";
    public const string LinkAdded = "link_added";
    public const string LinkRemoved = "link_removed";
}
