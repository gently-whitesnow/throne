using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using Throne.Application.Errors;

namespace Throne.Application.ChatUploads;

/// <summary>
/// Walks a chat-upload zip and verifies that every conversation declared in
/// the manifest has a matching entry inside the archive whose sha256 matches.
/// Operates on a seekable stream and rewinds it to 0 before returning so the
/// caller can hand it straight to the repository.
/// </summary>
public static class ChatUploadArchiveValidator
{
    public static void Validate(Stream archive, ChatUploadManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(manifest);

        if (!archive.CanSeek)
        {
            throw new InvalidOperationException("Chat-upload archive stream must be seekable.");
        }

        archive.Position = 0;

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new ApiException(
                ErrorCodes.ChatUploadArchiveInvalid,
                $"Uploaded archive is not a valid zip: {ex.Message}");
        }

        try
        {
            foreach (var conversation in manifest.Conversations)
            {
                var entry = zip.GetEntry(conversation.Path)
                    ?? throw new ApiException(
                        ErrorCodes.ChatUploadArchiveInvalid,
                        $"Archive is missing conversation file '{conversation.Path}' declared in the manifest.",
                        new Dictionary<string, object?>
                        {
                            ["conversation_id"] = conversation.Id,
                            ["path"] = conversation.Path,
                        });

                using var entryStream = entry.Open();
                var actualSha = ComputeSha256Hex(entryStream);
                if (!string.Equals(actualSha, conversation.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ApiException(
                        ErrorCodes.ChatUploadArchiveInvalid,
                        $"sha256 mismatch for conversation '{conversation.Id}'.",
                        new Dictionary<string, object?>
                        {
                            ["conversation_id"] = conversation.Id,
                            ["path"] = conversation.Path,
                            ["expected_sha256"] = conversation.Sha256.ToLowerInvariant(),
                            ["actual_sha256"] = actualSha,
                        });
                }
            }
        }
        finally
        {
            zip.Dispose();
        }

        archive.Position = 0;
    }

    private static string ComputeSha256Hex(Stream stream)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLower(CultureInfo.InvariantCulture);
    }
}
