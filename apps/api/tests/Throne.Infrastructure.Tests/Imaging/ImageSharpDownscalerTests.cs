using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Throne.Infrastructure.Imaging;

namespace Throne.Infrastructure.Tests.Imaging;

public class ImageSharpDownscalerTests
{
    [Fact(DisplayName = "ImageSharpDownscaler ресайзит большое PNG до max_dimension по длинной стороне")]
    public async Task Resizes_large_image()
    {
        var source = NewPng(width: 4000, height: 3000);
        var sut = new ImageSharpDownscaler();

        var result = await sut.DownscaleAsync(source, "image/png", maxDimension: 1024, CancellationToken.None);

        result.MimeType.Should().Be("image/jpeg");
        Math.Max(result.Width, result.Height).Should().Be(1024);
        result.Width.Should().Be(1024);
        result.Height.Should().Be(768);
        result.Data.Length.Should().BeLessThan((int)source.Length);
    }

    [Fact(DisplayName = "ImageSharpDownscaler не апскейлит изображения меньше max_dimension")]
    public async Task Does_not_upscale_small_image()
    {
        var source = NewPng(width: 320, height: 240);
        var sut = new ImageSharpDownscaler();

        var result = await sut.DownscaleAsync(source, "image/png", maxDimension: 1024, CancellationToken.None);

        result.Width.Should().Be(320);
        result.Height.Should().Be(240);
        result.MimeType.Should().Be("image/jpeg");
    }

    [Fact(DisplayName = "ImageSharpDownscaler перекодирует JPEG в JPEG того же размера если он меньше cap")]
    public async Task Recodes_small_jpeg_without_resize()
    {
        var source = NewJpeg(width: 600, height: 400);
        var sut = new ImageSharpDownscaler();

        var result = await sut.DownscaleAsync(source, "image/jpeg", maxDimension: 1024, CancellationToken.None);

        result.Width.Should().Be(600);
        result.Height.Should().Be(400);
    }

    private static MemoryStream NewPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(120, 160, 220));
        var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream NewJpeg(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(220, 200, 80));
        var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder { Quality = 90 });
        stream.Position = 0;
        return stream;
    }
}
