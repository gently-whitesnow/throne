using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentPinRowMapper
{
    public static IntentPin ToDomain(IntentPinRow row) => new(
        new IntentId(row.IntentId),
        new TagId(row.ContextTagId),
        row.PinSortKey,
        row.CreatedAt);
}
