using System.Text.Json.Serialization;

namespace Throne.Application.ChatUploads;

/// <summary>
/// Wire-format DTOs for <c>manifest.json</c>. Records with positional
/// initialization keep the per-type cyclomatic complexity flat (one setter
/// each is enough to fail CA1502 with many properties).
/// </summary>
internal static class ChatUploadManifestDtos
{
    public sealed record ManifestDto(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("agent")] string? Agent,
        [property: JsonPropertyName("agentVersion")] string? AgentVersion,
        [property: JsonPropertyName("device")] string? Device,
        [property: JsonPropertyName("deviceDisplayName")] string? DeviceDisplayName,
        [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,
        [property: JsonPropertyName("dateRange")] DateRangeDto? DateRange,
        [property: JsonPropertyName("conversations")] List<ConversationDto>? Conversations);

    public sealed record DateRangeDto(
        [property: JsonPropertyName("from")] DateTimeOffset? From,
        [property: JsonPropertyName("to")] DateTimeOffset? To);

    public sealed record ConversationDto(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("sha256")] string? Sha256,
        [property: JsonPropertyName("messageCount")] int? MessageCount,
        [property: JsonPropertyName("from")] DateTimeOffset? From,
        [property: JsonPropertyName("to")] DateTimeOffset? To,
        [property: JsonPropertyName("sizeBytes")] long? SizeBytes);
}
