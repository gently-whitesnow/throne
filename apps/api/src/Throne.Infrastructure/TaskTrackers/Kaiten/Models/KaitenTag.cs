namespace Throne.Infrastructure.TaskTrackers.Kaiten.Models;

/// <summary>A company-level tag definition.</summary>
internal sealed record KaitenTag(long Id, string Name, int Color, long? CompanyId, bool Archived);

/// <summary>A tag as attached to a specific card (links the card to the underlying tag).</summary>
internal sealed record KaitenCardTag(long Id, string Name, int Color, long CardId, long TagId);

/// <summary>
/// Payload for <c>POST /cards/{id}/tags</c>. Kaiten resolves the tag by name, creating it when the
/// name is new, so attaching is name-based rather than id-based.
/// </summary>
internal sealed record KaitenAddCardTagRequest(string Name);
