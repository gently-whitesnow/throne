using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentLinkRowMapper
{
    public static IntentLinkRow ToRow(IntentLink link) => new()
    {
        Id = link.Id,
        FromId = link.FromId.Value,
        ToId = link.ToId.Value,
        Blocking = link.Blocking,
        Author = link.Author.ToWire(),
        Rationale = link.Rationale,
        CreatedAt = link.CreatedAt,
    };

    public static IntentLink ToDomain(IntentLinkRow row) => new(
        Id: row.Id,
        FromId: new IntentId(row.FromId),
        ToId: new IntentId(row.ToId),
        Blocking: row.Blocking,
        Author: IntentLinkAuthorExtensions.FromWire(row.Author),
        Rationale: row.Rationale,
        CreatedAt: row.CreatedAt);
}
