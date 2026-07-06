namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Wire-format constants for <see cref="IntentCardAttachment"/> availability (ADR-0052). The value is
/// the recorded outcome of the last attach/refresh pull — never re-probed on read:
/// <list type="bullet">
///   <item><see cref="Available"/> — the last pull succeeded and the snapshot is authoritative.</item>
///   <item><see cref="Unavailable"/> — the tracker was unreachable / not connected at the last refresh;
///         the previous snapshot is kept.</item>
///   <item><see cref="Gone"/> — the card returned 404/403 upstream; the previous snapshot is kept.</item>
/// </list>
/// </summary>
public static class CardAvailabilityNames
{
    public const string Available = "available";
    public const string Unavailable = "unavailable";
    public const string Gone = "gone";

    public static bool IsKnown(string value) =>
        value is Available or Unavailable or Gone;
}
