using FluentAssertions;
using NSubstitute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Infrastructure.Imaging;

namespace Throne.Infrastructure.Tests.Imaging;

public class IntentAttachmentCompressionWorkerTests
{
    [Fact(DisplayName = "CompressBatchAsync сжимает все pending элементы и вызывает ApplyCompressionAsync")]
    public async Task Compresses_all_pending_items()
    {
        var item = new PendingCompressionItem("att-1", "gridfs-1", "image/png");
        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListPendingCompressionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { item });
        repo.OpenRawContentAsync("gridfs-1", Arg.Any<CancellationToken>())
            .Returns(NewPng(2000, 1500));

        var downscaler = new ImageSharpDownscaler();
        var settings = new IntentAttachmentCompressionOptions { MaxDimension = 768, BatchSize = 10, JpegQuality = 60 };

        await IntentAttachmentCompressionWorker
            .CompressBatchAsync(repo, downscaler, settings, log: null, CancellationToken.None);

        await repo.Received(1).ApplyCompressionAsync(
            "att-1",
            "gridfs-1",
            Arg.Is<DownscaledImage>(d =>
                d.MimeType == "image/jpeg" &&
                d.Width == 768 &&
                d.Height == 576 &&
                d.Data.Length > 0),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CompressBatchAsync пропускает элемент без блоба и не валит остальные")]
    public async Task Skips_missing_blob_and_continues()
    {
        var missing = new PendingCompressionItem("att-missing", "no-blob", "image/png");
        var ok = new PendingCompressionItem("att-ok", "blob-2", "image/png");
        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListPendingCompressionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { missing, ok });
        repo.OpenRawContentAsync("no-blob", Arg.Any<CancellationToken>()).Returns((Stream?)null);
        repo.OpenRawContentAsync("blob-2", Arg.Any<CancellationToken>()).Returns(NewPng(800, 600));

        var settings = new IntentAttachmentCompressionOptions { MaxDimension = 768, BatchSize = 10, JpegQuality = 60 };

        await IntentAttachmentCompressionWorker
            .CompressBatchAsync(repo, new ImageSharpDownscaler(), settings, log: null, CancellationToken.None);

        await repo.DidNotReceive().ApplyCompressionAsync(
            "att-missing", Arg.Any<string>(), Arg.Any<DownscaledImage>(), Arg.Any<CancellationToken>());
        await repo.Received(1).ApplyCompressionAsync(
            "att-ok", "blob-2", Arg.Any<DownscaledImage>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CompressBatchAsync ловит ошибку downscale на одном элементе и идёт дальше")]
    public async Task Survives_downscale_failure()
    {
        var bad = new PendingCompressionItem("att-bad", "blob-bad", "image/png");
        var ok = new PendingCompressionItem("att-ok", "blob-ok", "image/png");

        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListPendingCompressionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new[] { bad, ok });
        repo.OpenRawContentAsync("blob-bad", Arg.Any<CancellationToken>()).Returns(new MemoryStream([0xFF, 0xFE]));
        repo.OpenRawContentAsync("blob-ok", Arg.Any<CancellationToken>()).Returns(NewPng(500, 400));

        var settings = new IntentAttachmentCompressionOptions { MaxDimension = 768, BatchSize = 10, JpegQuality = 60 };

        await IntentAttachmentCompressionWorker
            .CompressBatchAsync(repo, new ImageSharpDownscaler(), settings, log: null, CancellationToken.None);

        await repo.DidNotReceive().ApplyCompressionAsync(
            "att-bad", Arg.Any<string>(), Arg.Any<DownscaledImage>(), Arg.Any<CancellationToken>());
        await repo.Received(1).ApplyCompressionAsync(
            "att-ok", "blob-ok", Arg.Any<DownscaledImage>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "CompressBatchAsync ничего не делает на пустом батче")]
    public async Task Empty_batch_is_noop()
    {
        var repo = Substitute.For<IIntentAttachmentRepository>();
        repo.ListPendingCompressionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PendingCompressionItem>());
        var downscaler = Substitute.For<IImageDownscaler>();

        await IntentAttachmentCompressionWorker.CompressBatchAsync(
            repo,
            downscaler,
            new IntentAttachmentCompressionOptions(),
            log: null,
            CancellationToken.None);

        await downscaler.DidNotReceiveWithAnyArgs().DownscaleAsync(default!, default!, default, default, default);
        await repo.DidNotReceiveWithAnyArgs().ApplyCompressionAsync(default!, default!, default!, default);
    }

    private static MemoryStream NewPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(120, 160, 220));
        var stream = new MemoryStream();
        image.Save(stream, new PngEncoder());
        stream.Position = 0;
        return stream;
    }
}
