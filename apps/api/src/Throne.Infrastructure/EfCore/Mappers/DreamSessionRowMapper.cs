using Throne.Domain.Dreams;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class DreamSessionRowMapper
{
    public static DreamSessionRow ToRow(DreamSession session) => new()
    {
        Id = session.Id,
        CreatedAt = session.Identity.CreatedAt,
        Vendor = session.Payload.Vendor,
        Host = session.Payload.Host,
        DateFrom = session.Payload.DateFrom,
        DateTo = session.Payload.DateTo,
        ProcessedConversationIds = session.Payload.ProcessedConversationIds.ToList(),
        Summary = session.Payload.Summary,
        Reflection = session.Payload.Reflection,
        ProposedPatchIds = session.Payload.ProposedPatchIds.ToList(),
    };

    public static DreamSession ToDomain(DreamSessionRow row) => DreamSession.Restore(
        id: row.Id,
        createdAt: row.CreatedAt,
        vendor: row.Vendor,
        host: row.Host,
        dateFrom: row.DateFrom,
        dateTo: row.DateTo,
        processedConversationIds: row.ProcessedConversationIds,
        summary: row.Summary,
        reflection: row.Reflection,
        proposedPatchIds: row.ProposedPatchIds);
}
