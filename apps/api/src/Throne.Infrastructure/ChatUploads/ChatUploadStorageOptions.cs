namespace Throne.Infrastructure.ChatUploads;

/// <summary>
/// Configuration for the host-volume directory that stores chat-history archives
/// (ADR-0015). The directory must exist or be creatable at startup; one zip file
/// per upload, named "&lt;id&gt;.zip".
/// </summary>
public sealed class ChatUploadStorageOptions
{
    public const string SectionName = "ChatUploads";

    public string StoragePath { get; set; } = "/var/lib/throne/chat-uploads";
}
