using System.Text.RegularExpressions;

namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Provider-qualified <c>(tracker, board_id, card_id)</c> coordinate of an attached card. Together with
/// <c>intent_id</c> forms the unique key (ADR-0052) — the same card cannot be attached twice to one
/// intent; a re-attach refreshes the existing snapshot.
///
/// <see cref="Tracker"/> is validated against the open wire-key shape (ADR-0046); <see cref="BoardId"/>
/// and <see cref="CardId"/> are opaque provider-native ids and only checked for non-emptiness. An invalid
/// component throws <see cref="ArgumentException"/>, surfaced by the service as a 422.
/// </summary>
public sealed partial record CardCoordinate
{
    public CardCoordinate(string Tracker, string BoardId, string CardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(BoardId);
        ArgumentException.ThrowIfNullOrWhiteSpace(CardId);

        if (!TrackerPattern().IsMatch(Tracker))
        {
            throw new ArgumentException(
                "tracker must match ^[a-z0-9][a-z0-9-]*$ (lowercase alphanumeric, hyphen; no leading hyphen).",
                nameof(Tracker));
        }

        this.Tracker = Tracker;
        this.BoardId = BoardId;
        this.CardId = CardId;
    }

    public string Tracker { get; }
    public string BoardId { get; }
    public string CardId { get; }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrackerPattern();
}
