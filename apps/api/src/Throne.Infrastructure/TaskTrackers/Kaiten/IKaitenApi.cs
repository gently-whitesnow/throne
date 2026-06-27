using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

/// <summary>
/// The Kaiten adapter's surface, grouped by resource. Every call takes the
/// <see cref="KaitenConnection"/> explicitly — the client holds no per-company state. This is the
/// seam the connection-settings and sync axes consume.
/// </summary>
internal interface IKaitenClient
{
    IKaitenTopologyApi Topology { get; }

    IKaitenCardsApi Cards { get; }

    IKaitenCommentsApi Comments { get; }

    IKaitenTagsApi Tags { get; }

    IKaitenCardChildrenApi CardChildren { get; }
}

/// <summary>Read access to the board structure: spaces, boards, columns and lanes.</summary>
internal interface IKaitenTopologyApi
{
    Task<IReadOnlyList<KaitenSpace>> ListSpacesAsync(KaitenConnection connection, CancellationToken ct);

    Task<KaitenSpace> GetSpaceAsync(KaitenConnection connection, long spaceId, CancellationToken ct);

    Task<IReadOnlyList<KaitenBoard>> ListBoardsAsync(KaitenConnection connection, long spaceId, CancellationToken ct);

    Task<KaitenBoard> GetBoardAsync(KaitenConnection connection, long boardId, CancellationToken ct);

    Task<IReadOnlyList<KaitenColumn>> ListColumnsAsync(KaitenConnection connection, long boardId, CancellationToken ct);

    Task<IReadOnlyList<KaitenLane>> ListLanesAsync(KaitenConnection connection, long boardId, CancellationToken ct);
}

/// <summary>Card CRUD. Move is an update that sets <c>column_id</c> / <c>lane_id</c>.</summary>
internal interface IKaitenCardsApi
{
    Task<IReadOnlyList<KaitenCard>> ListCardsAsync(KaitenConnection connection, KaitenCardQuery query, CancellationToken ct);

    Task<IReadOnlyList<KaitenCard>> ListAllCardsAsync(KaitenConnection connection, KaitenCardQuery query, CancellationToken ct);

    Task<KaitenCard> GetCardAsync(KaitenConnection connection, long cardId, CancellationToken ct);

    Task<KaitenCard> CreateCardAsync(KaitenConnection connection, KaitenCreateCardRequest request, CancellationToken ct);

    Task<KaitenCard> UpdateCardAsync(KaitenConnection connection, long cardId, KaitenUpdateCardRequest request, CancellationToken ct);
}

/// <summary>Card comments.</summary>
internal interface IKaitenCommentsApi
{
    Task<IReadOnlyList<KaitenComment>> ListCommentsAsync(KaitenConnection connection, long cardId, CancellationToken ct);

    Task<KaitenComment> AddCommentAsync(KaitenConnection connection, long cardId, KaitenCreateCommentRequest request, CancellationToken ct);
}

/// <summary>Company tags and card-tag attachments.</summary>
internal interface IKaitenTagsApi
{
    Task<IReadOnlyList<KaitenTag>> ListCompanyTagsAsync(KaitenConnection connection, CancellationToken ct);

    Task<IReadOnlyList<KaitenCardTag>> ListCardTagsAsync(KaitenConnection connection, long cardId, CancellationToken ct);

    Task<KaitenTag> AddCardTagAsync(KaitenConnection connection, long cardId, KaitenAddCardTagRequest request, CancellationToken ct);

    Task RemoveCardTagAsync(KaitenConnection connection, long cardId, long tagId, CancellationToken ct);
}

/// <summary>
/// Parent/child card hierarchy. Documented in Kaiten as the <c>card-children</c> group; the REST
/// surface is the nested cards path (<c>/cards/{parent_id}/children</c>).
/// </summary>
internal interface IKaitenCardChildrenApi
{
    Task<IReadOnlyList<KaitenCard>> ListChildrenAsync(KaitenConnection connection, long cardId, CancellationToken ct);

    Task<KaitenCard> AddChildAsync(KaitenConnection connection, long cardId, KaitenAddCardChildRequest request, CancellationToken ct);

    Task RemoveChildAsync(KaitenConnection connection, long cardId, long childCardId, CancellationToken ct);
}
