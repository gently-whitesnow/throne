using Throne.ChatUploads.Contracts.Generated;
using AppChatUpload = Throne.Application.ChatUploads.ChatUpload;
using AppStatusNames = Throne.Application.ChatUploads.ChatUploadStatusNames;

namespace Throne.Api.ChatUploads;

internal static class ChatUploadDtoMapper
{
    public static ChatUploadDto ToDto(AppChatUpload upload) => new()
    {
        Id = upload.Id,
        Agent = upload.Agent,
        Agent_version = upload.AgentVersion,
        Device = upload.Device,
        Device_display_name = upload.DeviceDisplayName,
        Date_range = new ChatUploadDateRangeDto
        {
            From = upload.DateRangeFrom,
            To = upload.DateRangeTo,
        },
        Conversation_count = upload.ConversationCount,
        Size_bytes = upload.SizeBytes,
        Status = upload.Status switch
        {
            AppStatusNames.Uploaded => ChatUploadStatus.Uploaded,
            _ => ChatUploadStatus.Uploaded,
        },
        Created_at = upload.CreatedAt,
    };
}
