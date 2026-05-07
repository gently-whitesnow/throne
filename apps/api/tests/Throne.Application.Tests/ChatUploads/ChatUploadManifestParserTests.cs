using FluentAssertions;
using Throne.Application.ChatUploads;
using Throne.Application.Errors;

namespace Throne.Application.Tests.ChatUploads;

public class ChatUploadManifestParserTests
{
    private const string ValidManifest = """
        {
          "schemaVersion": 1,
          "agent": "claude-code",
          "agentVersion": "1.2.3",
          "device": "gently@MacBook-Pro",
          "deviceDisplayName": "MacBook Pro",
          "createdAt": "2026-05-07T19:41:05Z",
          "dateRange": { "from": "2026-04-01T08:00:00Z", "to": "2026-05-07T19:00:00Z" },
          "conversations": [
            {
              "id": "claude-code-abc123",
              "path": "projects/throne/abc123.jsonl",
              "sha256": "deadbeef",
              "messageCount": 42,
              "from": "2026-04-15T10:00:00Z",
              "to":   "2026-04-15T13:30:00Z",
              "sizeBytes": 123456
            }
          ]
        }
        """;

    [Fact(DisplayName = "Парсит валидный manifest целиком, включая опциональные поля")]
    public void Parses_full_manifest()
    {
        var manifest = ChatUploadManifestParser.Parse(ValidManifest);

        manifest.SchemaVersion.Should().Be(1);
        manifest.Agent.Should().Be("claude-code");
        manifest.AgentVersion.Should().Be("1.2.3");
        manifest.Device.Should().Be("gently@MacBook-Pro");
        manifest.DeviceDisplayName.Should().Be("MacBook Pro");
        manifest.Conversations.Should().HaveCount(1);
        manifest.Conversations[0].Sha256.Should().Be("deadbeef");
        manifest.Conversations[0].MessageCount.Should().Be(42);
    }

    [Fact(DisplayName = "Пустые опциональные поля приходят как null")]
    public void Empty_optional_fields_become_null()
    {
        var json = """
            {
              "schemaVersion": 1,
              "agent": "codex-cli",
              "device": "user@host",
              "createdAt": "2026-05-07T19:41:05Z",
              "dateRange": { "from": "2026-05-07T19:00:00Z", "to": "2026-05-07T19:30:00Z" },
              "conversations": []
            }
            """;

        var manifest = ChatUploadManifestParser.Parse(json);

        manifest.AgentVersion.Should().BeNull();
        manifest.DeviceDisplayName.Should().BeNull();
        manifest.Conversations.Should().BeEmpty();
    }

    [Fact(DisplayName = "Неподдерживаемый schemaVersion даёт chat_upload.schema_unsupported")]
    public void Rejects_unsupported_schema_version()
    {
        var json = ValidManifest.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal);

        var act = () => ChatUploadManifestParser.Parse(json);

        act.Should().Throw<ApiException>()
            .Where(e => e.Code == ErrorCodes.ChatUploadSchemaUnsupported);
    }

    [Fact(DisplayName = "Невалидный JSON даёт chat_upload.manifest_invalid")]
    public void Rejects_malformed_json()
    {
        var act = () => ChatUploadManifestParser.Parse("{ not json");

        act.Should().Throw<ApiException>()
            .Where(e => e.Code == ErrorCodes.ChatUploadManifestInvalid);
    }

    [Fact(DisplayName = "Отсутствие conversations даёт chat_upload.manifest_invalid")]
    public void Rejects_missing_conversations()
    {
        var json = """
            {
              "schemaVersion": 1,
              "agent": "claude-code",
              "device": "u@h",
              "createdAt": "2026-05-07T19:41:05Z",
              "dateRange": { "from": "2026-05-07T19:00:00Z", "to": "2026-05-07T19:30:00Z" }
            }
            """;

        var act = () => ChatUploadManifestParser.Parse(json);

        act.Should().Throw<ApiException>()
            .Where(e => e.Code == ErrorCodes.ChatUploadManifestInvalid);
    }

    [Fact(DisplayName = "dateRange.to раньше from даёт chat_upload.manifest_invalid")]
    public void Rejects_inverted_date_range()
    {
        var json = ValidManifest.Replace(
            "\"from\": \"2026-04-01T08:00:00Z\", \"to\": \"2026-05-07T19:00:00Z\"",
            "\"from\": \"2026-05-07T19:00:00Z\", \"to\": \"2026-04-01T08:00:00Z\"",
            StringComparison.Ordinal);

        var act = () => ChatUploadManifestParser.Parse(json);

        act.Should().Throw<ApiException>()
            .Where(e => e.Code == ErrorCodes.ChatUploadManifestInvalid);
    }
}
