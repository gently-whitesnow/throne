using Dtos = Throne.Application.ChatUploads.ChatUploadManifestDtos;

namespace Throne.Application.ChatUploads;

internal static class ChatUploadManifestDtoMapper
{
    public static ChatUploadManifest Map(Dtos.ManifestDto dto)
    {
        var dateRange = dto.DateRange ?? throw Fail("dateRange is required.");
        var rangeFrom = dateRange.From ?? throw Fail("dateRange.from is required and must be ISO-8601.");
        var rangeTo = dateRange.To ?? throw Fail("dateRange.to is required and must be ISO-8601.");
        if (rangeTo < rangeFrom)
        {
            throw Fail("dateRange.to must not precede dateRange.from.");
        }

        var conversations = dto.Conversations ?? throw Fail("conversations is required.");
        var mapped = new List<ChatUploadConversation>(conversations.Count);
        for (var i = 0; i < conversations.Count; i++)
        {
            mapped.Add(MapConversation(conversations[i], i));
        }

        return new ChatUploadManifest(
            SchemaVersion: ChatUploadLimits.CurrentSchemaVersion,
            Agent: NonEmpty(dto.Agent, "agent"),
            AgentVersion: Optional(dto.AgentVersion),
            Device: NonEmpty(dto.Device, "device"),
            DeviceDisplayName: Optional(dto.DeviceDisplayName),
            CreatedAt: dto.CreatedAt ?? throw Fail("createdAt is required and must be ISO-8601."),
            DateRange: new ChatUploadDateRange(rangeFrom, rangeTo),
            Conversations: mapped);
    }

    private static ChatUploadConversation MapConversation(Dtos.ConversationDto dto, int index) => new(
        Id: NonEmpty(dto.Id, $"conversations[{index}].id"),
        Path: NonEmpty(dto.Path, $"conversations[{index}].path"),
        Sha256: NonEmpty(dto.Sha256, $"conversations[{index}].sha256"),
        MessageCount: dto.MessageCount ?? throw Fail($"conversations[{index}].messageCount is required."),
        From: dto.From ?? throw Fail($"conversations[{index}].from is required and must be ISO-8601."),
        To: dto.To ?? throw Fail($"conversations[{index}].to is required and must be ISO-8601."),
        SizeBytes: dto.SizeBytes ?? throw Fail($"conversations[{index}].sizeBytes is required."));

    private static string NonEmpty(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw Fail($"{field} is required and must be a non-empty string.")
            : value;

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static Errors.ApiException Fail(string detail) =>
        ChatUploadManifestParser.Invalid("Manifest." + detail);
}
