namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Wire-format constants for <c>intent_events.kind</c>. Used by the partial unique index
/// HasFilter, which is raw SQL outside the domain assembly.
/// </summary>
internal static class IntentEventKindWires
{
    public const string TextChanged = "text_changed";
    public const string LinkAdded = "link_added";
    public const string LinkRemoved = "link_removed";
}
