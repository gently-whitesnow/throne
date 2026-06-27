namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Lifecycle of a <see cref="CardSyncLink"/>. <c>linked</c> is the live mirror; <c>stub</c> means the
/// upstream card vanished or became unreadable, so the mirror intent is kept but its content is no
/// longer overwritten from the tracker until the link recovers.
/// </summary>
public static class CardSyncLinkState
{
    public const string Linked = "linked";
    public const string Stub = "stub";

    public static readonly IReadOnlyList<string> All = [Linked, Stub];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}
