using System.Text.RegularExpressions;

namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Pointer to an external task-tracker card (Kaiten / Jira / Linear / … — ADR-0045/0046).
/// Throne keeps only the stable identifiers needed to round-trip to the upstream card; the
/// mutable content (title, description, column, …) lives in the tracker and is read through the
/// provider adapter. This value object carries no connection secret — base URL / token belong to
/// the connection-settings axis, not the link.
/// </summary>
/// <remarks>
/// The <see cref="Tracker"/> key is an open wire key (ADR-0046): its <em>shape</em> is validated
/// here, but whether a tracker is actually registered is resolved against the provider registry at
/// the server boundary, not in the domain — the core stays provider-neutral with no hardcoded list.
/// </remarks>
public sealed record TaskTrackerCardLink
{
    private static readonly Regex TrackerKeyShape =
        new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public TaskTrackerCardLink(string tracker, string boardId, string cardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardId);

        if (!TrackerKeyShape.IsMatch(tracker))
        {
            throw new ArgumentException(
                $"Tracker key '{tracker}' must match {TrackerKeyShape}.",
                nameof(tracker));
        }

        Tracker = tracker;
        BoardId = boardId;
        CardId = cardId;
    }

    /// <summary>Stable provider key — the registry key of the owning <c>ITaskTrackerProvider</c>.</summary>
    public string Tracker { get; }

    /// <summary>Board/space identifier within the tracker that scopes <see cref="CardId"/>.</summary>
    public string BoardId { get; }

    /// <summary>Card/issue identifier within <see cref="BoardId"/>.</summary>
    public string CardId { get; }
}
