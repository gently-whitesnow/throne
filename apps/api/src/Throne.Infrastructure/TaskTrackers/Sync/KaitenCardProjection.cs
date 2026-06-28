using System.Globalization;
using Throne.Application.TaskTrackers.Sync;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Sync;

/// <summary>
/// Projects a native <see cref="KaitenCard"/> onto the provider-neutral <see cref="TaskTrackerCard"/>,
/// resolving the column title from the board's column list. Keeps every Kaiten-internal type behind the
/// adapter boundary, exactly as the topology/connection projections already do.
/// </summary>
internal static class KaitenCardProjection
{
    public static TaskTrackerCard ToCard(KaitenCard card, IReadOnlyDictionary<long, string> columnTitles) =>
        new(
            CardId: card.Id.ToString(CultureInfo.InvariantCulture),
            BoardId: card.BoardId.ToString(CultureInfo.InvariantCulture),
            ColumnId: card.ColumnId.ToString(CultureInfo.InvariantCulture),
            ColumnTitle: columnTitles.GetValueOrDefault(card.ColumnId),
            Title: card.Title,
            Description: card.Description,
            UpdatedAt: card.Updated,
            ColumnChangedAt: card.ColumnChangedAt,
            Archived: card.Condition != KaitenCardConditions.Live);

    public static long ParseId(string value) => long.Parse(value, CultureInfo.InvariantCulture);
}

/// <summary>Kaiten's <c>condition</c> encoding: 1 = live, 2 = archived.</summary>
internal static class KaitenCardConditions
{
    public const int Live = 1;
    public const int Archived = 2;
}
