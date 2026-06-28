using System.Net;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Sync;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Sync;

/// <summary>
/// The Kaiten card I/O surface for two-way sync, adapting the provider-neutral
/// <see cref="ITaskTrackerSyncProvider"/> calls onto <see cref="IKaitenClient"/>. Registered only as a
/// sync provider (never as a catalog <see cref="ITaskTrackerProvider"/>) so it does not collide with
/// <see cref="KaitenTaskTrackerProvider"/>'s <c>kaiten</c> registry key.
/// </summary>
internal sealed class KaitenSyncProvider(IKaitenClient client) : ITaskTrackerSyncProvider
{
    public string TrackerKey => "kaiten";

    public string DisplayName => "Kaiten";

    public async Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection, string boardId, CancellationToken ct)
    {
        var kaiten = ToConnection(connection);
        var board = KaitenCardProjection.ParseId(boardId);
        var columnTitles = await LoadColumnTitlesAsync(kaiten, board, ct);
        var cards = await client.Cards.ListAllCardsAsync(
            kaiten, new KaitenCardQuery(BoardId: board, Condition: KaitenCardConditions.Live), ct);
        return cards
            .Where(c => c.Condition == KaitenCardConditions.Live)
            .Select(c => KaitenCardProjection.ToCard(c, columnTitles))
            .ToList();
    }

    public async Task<TaskTrackerCard?> GetCardAsync(
        TaskTrackerConnectionDescriptor connection, string cardId, CancellationToken ct)
    {
        var kaiten = ToConnection(connection);
        try
        {
            var card = await client.Cards.GetCardAsync(kaiten, KaitenCardProjection.ParseId(cardId), ct);
            var columnTitles = await LoadColumnTitlesAsync(kaiten, card.BoardId, ct);
            return KaitenCardProjection.ToCard(card, columnTitles);
        }
        catch (KaitenApiException ex) when (IsGone(ex.StatusCode))
        {
            return null;
        }
    }

    public async Task<TaskTrackerCard> UpdateCardAsync(
        TaskTrackerConnectionDescriptor connection, string cardId, TaskTrackerCardPatch patch, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var kaiten = ToConnection(connection);
        var request = new KaitenUpdateCardRequest(
            Title: patch.Title,
            Description: patch.Description,
            ColumnId: patch.ColumnId is null ? null : KaitenCardProjection.ParseId(patch.ColumnId));
        var card = await client.Cards.UpdateCardAsync(kaiten, KaitenCardProjection.ParseId(cardId), request, ct);
        var columnTitles = await LoadColumnTitlesAsync(kaiten, card.BoardId, ct);
        return KaitenCardProjection.ToCard(card, columnTitles);
    }

    public async Task<IReadOnlyList<string>> ListChildCardIdsAsync(
        TaskTrackerConnectionDescriptor connection, string parentCardId, CancellationToken ct)
    {
        var kaiten = ToConnection(connection);
        var children = await client.CardChildren.ListChildrenAsync(kaiten, KaitenCardProjection.ParseId(parentCardId), ct);
        return children.Select(c => c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
    }

    public Task AddChildAsync(TaskTrackerConnectionDescriptor connection, string parentCardId, string childCardId, CancellationToken ct) =>
        client.CardChildren.AddChildAsync(
            ToConnection(connection),
            KaitenCardProjection.ParseId(parentCardId),
            KaitenAddCardChildRequest.ForChild(KaitenCardProjection.ParseId(childCardId)),
            ct);

    public Task RemoveChildAsync(TaskTrackerConnectionDescriptor connection, string parentCardId, string childCardId, CancellationToken ct) =>
        client.CardChildren.RemoveChildAsync(
            ToConnection(connection),
            KaitenCardProjection.ParseId(parentCardId),
            KaitenCardProjection.ParseId(childCardId),
            ct);

    private async Task<IReadOnlyDictionary<long, string>> LoadColumnTitlesAsync(
        KaitenConnection kaiten, long boardId, CancellationToken ct)
    {
        var columns = await client.Topology.ListColumnsAsync(kaiten, boardId, ct);
        return columns.ToDictionary(c => c.Id, c => c.Title);
    }

    private static KaitenConnection ToConnection(TaskTrackerConnectionDescriptor connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new KaitenConnection(connection.BaseUrl, connection.Token);
    }

    private static bool IsGone(HttpStatusCode status) =>
        status is HttpStatusCode.NotFound or HttpStatusCode.Forbidden;
}
