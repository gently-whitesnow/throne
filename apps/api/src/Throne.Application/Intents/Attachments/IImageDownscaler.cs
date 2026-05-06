namespace Throne.Application.Intents.Attachments;

public sealed record DownscaledImage(byte[] Data, string MimeType, int Width, int Height);

public interface IImageDownscaler
{
    Task<DownscaledImage> DownscaleAsync(
        Stream source,
        string sourceMime,
        int maxDimension,
        CancellationToken ct);
}
