using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Throne.Application.ChatUploads;
using Throne.Application.Errors;

namespace Throne.Application.Tests.ChatUploads;

public class ChatUploadArchiveValidatorTests
{
    [Fact(DisplayName = "Принимает архив, в котором каждый объявленный диалог сходится по sha256")]
    public void Accepts_matching_archive()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}\n");
        var archive = BuildArchive([("conv1.jsonl", bytes)]);

        var manifest = ManifestWith(
            new ChatUploadConversation(
                Id: "c1",
                Path: "conv1.jsonl",
                Sha256: Sha256Hex(bytes),
                MessageCount: 1,
                From: DateTimeOffset.UtcNow,
                To: DateTimeOffset.UtcNow,
                SizeBytes: bytes.Length));

        var act = () => ChatUploadArchiveValidator.Validate(archive, manifest);
        act.Should().NotThrow();
        archive.Position.Should().Be(0);
    }

    [Fact(DisplayName = "Отсутствие объявленного файла даёт chat_upload.archive_invalid")]
    public void Rejects_missing_file()
    {
        var archive = BuildArchive([("other.jsonl", [1, 2, 3])]);

        var manifest = ManifestWith(
            new ChatUploadConversation(
                Id: "c1",
                Path: "missing.jsonl",
                Sha256: "deadbeef",
                MessageCount: 0,
                From: DateTimeOffset.UtcNow,
                To: DateTimeOffset.UtcNow,
                SizeBytes: 0));

        var act = () => ChatUploadArchiveValidator.Validate(archive, manifest);
        act.Should().Throw<ApiException>()
            .Where(e => e.Code == ErrorCodes.ChatUploadArchiveInvalid);
    }

    [Fact(DisplayName = "Расхождение sha256 даёт chat_upload.archive_invalid")]
    public void Rejects_sha_mismatch()
    {
        var actual = Encoding.UTF8.GetBytes("real");
        var archive = BuildArchive([("conv.jsonl", actual)]);

        var manifest = ManifestWith(
            new ChatUploadConversation(
                Id: "c1",
                Path: "conv.jsonl",
                Sha256: Sha256Hex(Encoding.UTF8.GetBytes("different")),
                MessageCount: 0,
                From: DateTimeOffset.UtcNow,
                To: DateTimeOffset.UtcNow,
                SizeBytes: actual.Length));

        var act = () => ChatUploadArchiveValidator.Validate(archive, manifest);
        act.Should().Throw<ApiException>()
            .Where(e => e.Code == ErrorCodes.ChatUploadArchiveInvalid);
    }

    [Fact(DisplayName = "Не-zip-поток даёт chat_upload.archive_invalid")]
    public void Rejects_non_zip()
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain text, not a zip"));

        var act = () => ChatUploadArchiveValidator.Validate(stream, ManifestWith());
        act.Should().Throw<ApiException>()
            .Where(e => e.Code == ErrorCodes.ChatUploadArchiveInvalid);
    }

    private static MemoryStream BuildArchive(IEnumerable<(string path, byte[] bytes)> entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, bytes) in entries)
            {
                var entry = zip.CreateEntry(path);
                using var s = entry.Open();
                s.Write(bytes, 0, bytes.Length);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static ChatUploadManifest ManifestWith(params ChatUploadConversation[] conversations) => new(
        SchemaVersion: 1,
        Agent: "claude-code",
        AgentVersion: null,
        Device: "u@h",
        DeviceDisplayName: null,
        CreatedAt: DateTimeOffset.UtcNow,
        DateRange: new ChatUploadDateRange(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        Conversations: conversations);

    private static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
