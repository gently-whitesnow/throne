using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentAttachmentDtoMapper
{
    public static IntentAttachmentDto ToAttachmentDto(IntentAttachment attachment) => new()
    {
        Id = attachment.Id,
        Intent_id = attachment.IntentId,
        File_name = attachment.FileName,
        Content_type = attachment.ContentType,
        Size_bytes = attachment.SizeBytes,
        Created_at = attachment.CreatedAt,
    };
}
