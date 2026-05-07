namespace Throne.Application.ChatUploads;

public static class ChatUploadLimits
{
    public const long MaxArchiveBytes = 200L * 1024 * 1024; // 200 MB
    public const int MaxManifestBytes = 4 * 1024 * 1024;    // 4 MB — generous, manifest is JSON metadata.
    public const int CurrentSchemaVersion = 1;
}
