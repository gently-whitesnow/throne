using Throne.Domain.Intents.Training;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentStatusChangeRowMapper
{
    public static IntentStatusChangeRow ToRow(IntentStatusChange change) => new()
    {
        Id = change.Id,
        IntentId = change.IntentId.Value,
        IntentVersionAtWrite = change.IntentVersionAtWrite,
        FromStatus = change.FromStatus,
        ToStatus = change.ToStatus,
        Source = change.Source,
        CreatedAt = change.Audit.CreatedAt,
        CreatedBy = change.Audit.CreatedBy.ToWire(),
        Reason = change.Reason,
    };
}
