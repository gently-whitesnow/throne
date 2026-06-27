namespace Throne.Infrastructure.TaskTrackers.Kaiten.Models;

/// <summary>
/// Payload for adding a child to a card (<c>POST /cards/{parent_id}/children</c>). Per Kaiten's wire
/// shape the body field is <c>card_id</c> and it carries the <em>child</em> card id, while the parent
/// is in the path — hence the deliberately named property.
/// </summary>
internal sealed record KaitenAddCardChildRequest(long CardId)
{
    public static KaitenAddCardChildRequest ForChild(long childCardId) => new(childCardId);
}
