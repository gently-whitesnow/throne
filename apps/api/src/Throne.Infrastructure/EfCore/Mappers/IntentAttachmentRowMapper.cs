using Throne.Application.Intents;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentAttachmentRowMapper
{
    public const string CompressionStatePending = "pending";
    public const string CompressionStateReady = "ready";

    public static IntentAttachment ToDomain(IntentAttachmentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var isReady = string.Equals(row.CompressionState, CompressionStateReady, StringComparison.Ordinal);
        return new IntentAttachment(
            row.Id,
            row.IntentId,
            row.FileName,
            row.ContentType,
            row.SizeBytes,
            row.CreatedAt,
            isReady,
            isReady ? row.DerivedWidth : null,
            isReady ? row.DerivedHeight : null);
    }
}
