namespace Throne.Application.Intents;

public static class IntentAttachmentLimits
{
    public const int MaxPerIntent = 10;
    public const long MaxBytesPerFile = 10L * 1024 * 1024;
    public const int CompressedMaxDimension = 1024;
}
