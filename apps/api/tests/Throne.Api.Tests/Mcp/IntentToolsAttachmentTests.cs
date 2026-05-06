using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Throne.Api.Mcp.Tools;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Tests.Mcp;

public class IntentToolsAttachmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "get_intent возвращает только метаданные вложений, без image-блоков")]
    public async Task GetIntent_returns_only_text_block_and_attachment_metadata()
    {
        var intentId = IntentId.New();
        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(new List<IntentAttachment>
            {
                new("att-1", "user-1", intentId.Value, "shot1.png", "image/png", 12345, Now),
                new("att-2", "user-1", intentId.Value, "shot2.png", "image/png", 67890, Now),
            });

        var tagRepo = Substitute.For<ITagRepository>();
        tagRepo.ListAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var downscaler = Substitute.For<IImageDownscaler>();

        var tools = NewTools(intentRepo, attachments, downscaler, tagRepo);

        var result = await tools.GetIntent(intentId.Value, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().BeOfType<TextContentBlock>();
        await downscaler.DidNotReceiveWithAnyArgs()
            .DownscaleAsync(default!, default!, default, default);
        await attachments.DidNotReceiveWithAnyArgs()
            .OpenContentAsync(default!, default!, default);
    }

    [Fact(DisplayName = "get_intent_attachment_image отдаёт image-блок и метаданные ресайза")]
    public async Task GetIntentAttachmentImage_returns_image_block()
    {
        var intentId = IntentId.New();
        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var att = new IntentAttachment("att-1", "user-1", intentId.Value, "shot.png", "image/png", 100, Now);
        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.OpenContentAsync(Arg.Any<IntentId>(), "att-1", Arg.Any<CancellationToken>())
            .Returns(new IntentAttachmentContent(att, new MemoryStream([0x89, 0x50, 0x4E, 0x47])));

        var downscaler = Substitute.For<IImageDownscaler>();
        downscaler.DownscaleAsync(Arg.Any<Stream>(), "image/png", 1024, Arg.Any<CancellationToken>())
            .Returns(new DownscaledImage([1, 2, 3], "image/jpeg", 800, 600));

        var tagRepo = Substitute.For<ITagRepository>();

        var tools = NewTools(intentRepo, attachments, downscaler, tagRepo);

        var result = await tools.GetIntentAttachmentImage(intentId.Value, "att-1", CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().HaveCount(2);
        var image = result.Content.OfType<ImageContentBlock>().Single();
        image.MimeType.Should().Be("image/jpeg");
        image.Data.Should().Be(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        var meta = JsonSerializer.Deserialize<JsonElement>(((TextContentBlock)result.Content[0]).Text);
        meta.GetProperty("width").GetInt32().Should().Be(800);
        meta.GetProperty("height").GetInt32().Should().Be(600);
        meta.GetProperty("contentType").GetString().Should().Be("image/jpeg");
        meta.GetProperty("sourceContentType").GetString().Should().Be("image/png");
    }

    [Fact(DisplayName = "get_intent_attachment_image кидает intent.attachment.not_image для текстовых файлов")]
    public async Task GetIntentAttachmentImage_throws_for_non_image()
    {
        var intentId = IntentId.New();
        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var att = new IntentAttachment("att-1", "user-1", intentId.Value, "log.txt", "text/plain", 100, Now);
        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.OpenContentAsync(Arg.Any<IntentId>(), "att-1", Arg.Any<CancellationToken>())
            .Returns(new IntentAttachmentContent(att, new MemoryStream([1, 2, 3])));

        var tools = NewTools(intentRepo, attachments, Substitute.For<IImageDownscaler>(), Substitute.For<ITagRepository>());

        var act = () => tools.GetIntentAttachmentImage(intentId.Value, "att-1", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentAttachmentNotImage);
    }

    [Fact(DisplayName = "get_intent_attachment_image кидает intent.attachment.not_found если вложения нет")]
    public async Task GetIntentAttachmentImage_throws_when_missing()
    {
        var intentId = IntentId.New();
        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var attachments = Substitute.For<IIntentAttachmentRepository>();
        attachments.OpenContentAsync(Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IntentAttachmentContent?)null);

        var tools = NewTools(intentRepo, attachments, Substitute.For<IImageDownscaler>(), Substitute.For<ITagRepository>());

        var act = () => tools.GetIntentAttachmentImage(intentId.Value, "missing", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentAttachmentNotFound);
    }

    private static IntentTools NewTools(
        IIntentRepository intentRepo,
        IIntentAttachmentRepository attachments,
        IImageDownscaler downscaler,
        ITagRepository tagRepo) =>
        new(
            create: null!,
            get: new GetIntentHandler(intentRepo),
            read: null!,
            replace: null!,
            insertAfterLine: null!,
            search: null!,
            addQa: null!,
            addReview: null!,
            setStatus: null!,
            setTagsHandler: null!,
            getInstructionBundle: null!,
            attachments: attachments,
            imageDownscaler: downscaler,
            tagRepository: tagRepo);
}
