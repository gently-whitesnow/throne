using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Throne.Application.Intents.Attachments;

namespace Throne.Infrastructure.Imaging;

public sealed class ImageSharpDownscaler : IImageDownscaler
{
    private const string JpegMime = "image/jpeg";
    private const int JpegQuality = 85;

    public async Task<DownscaledImage> DownscaleAsync(
        Stream source,
        string sourceMime,
        int maxDimension,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maxDimension < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDimension), maxDimension, "max_dimension must be >= 1.");
        }

        using var buffered = new MemoryStream();
        await source.CopyToAsync(buffered, ct);
        buffered.Position = 0;

        using var image = await Image.LoadAsync(buffered, ct);
        var longest = Math.Max(image.Width, image.Height);
        if (longest > maxDimension)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxDimension, maxDimension),
                Sampler = KnownResamplers.Lanczos3,
            }));
        }

        using var output = new MemoryStream();
        await image.SaveAsync(output, new JpegEncoder { Quality = JpegQuality }, ct);
        return new DownscaledImage(output.ToArray(), JpegMime, image.Width, image.Height);
    }
}
