using System.Text.Json;
using System.Text.Json.Serialization;
using Throne.Application.Errors;

namespace Throne.Application.ChatUploads;

/// <summary>
/// Parses the JSON manifest that accompanies every chat-upload archive.
/// Throws <see cref="ApiException"/> with <c>chat_upload.manifest_invalid</c> /
/// <c>chat_upload.schema_unsupported</c> on malformed or unsupported input.
/// </summary>
public static class ChatUploadManifestParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static ChatUploadManifest Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw Invalid("Manifest body is empty.");
        }

        ChatUploadManifestDtos.ManifestDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ChatUploadManifestDtos.ManifestDto>(json, Options);
        }
        catch (JsonException ex)
        {
            throw Invalid($"Manifest is not valid JSON: {ex.Message}");
        }

        if (dto is null)
        {
            throw Invalid("Manifest is empty.");
        }

        if (dto.SchemaVersion != ChatUploadLimits.CurrentSchemaVersion)
        {
            throw new ApiException(
                ErrorCodes.ChatUploadSchemaUnsupported,
                $"Unsupported manifest.schemaVersion={dto.SchemaVersion}. Server expects {ChatUploadLimits.CurrentSchemaVersion}.",
                new Dictionary<string, object?>
                {
                    ["schema_version"] = dto.SchemaVersion,
                    ["expected_schema_version"] = ChatUploadLimits.CurrentSchemaVersion,
                });
        }

        return ChatUploadManifestDtoMapper.Map(dto);
    }

    internal static ApiException Invalid(string detail) => new(
        ErrorCodes.ChatUploadManifestInvalid,
        detail);
}
