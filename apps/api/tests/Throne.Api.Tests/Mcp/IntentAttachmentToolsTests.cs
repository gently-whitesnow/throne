using System.Text;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Throne.Api.Mcp.Tools;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Tests.Mcp;

public class IntentAttachmentToolsTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "read_intent_attachment_image возвращает один ImageContentBlock с base64 байтов и mime")]
    public async Task ReadImage_returns_image_content_block()
    {
        var intentId = IntentId.New();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var att = new IntentAttachment(
            "att-image", "user-1", intentId.Value, "shot.jpg", "image/jpeg", bytes.Length, Now,
            IsCompressed: true, CompressedWidth: 1024, CompressedHeight: 768);

        var tools = NewTools(intentId, att, bytes);

        var result = await tools.ReadIntentAttachmentImage(intentId.Value, "att-image", CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Content.Should().HaveCount(1);
        var block = result.Content[0].Should().BeOfType<ImageContentBlock>().Subject;
        block.MimeType.Should().Be("image/jpeg");
        block.Data.Should().Be(Convert.ToBase64String(bytes));
    }

    [Fact(DisplayName = "read_intent_attachment_image на text-аттаче бросает validation.failed с подсказкой text-tool")]
    public async Task ReadImage_on_text_attachment_throws_validation()
    {
        var intentId = IntentId.New();
        var att = new IntentAttachment(
            "att-log", "user-1", intentId.Value, "trace.log", "text/x-log", 4, Now);
        var tools = NewTools(intentId, att, [1, 2, 3, 4]);

        var act = () => tools.ReadIntentAttachmentImage(intentId.Value, "att-log", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Message.Should().Contain("read_intent_attachment_text");
    }

    [Fact(DisplayName = "read_intent_attachment_image на > 5 МБ бросает intent.attachment.too_large")]
    public async Task ReadImage_too_large_throws()
    {
        var intentId = IntentId.New();
        var bytes = new byte[5 * 1024 * 1024 + 1];
        var att = new IntentAttachment(
            "att-big", "user-1", intentId.Value, "big.jpg", "image/jpeg", bytes.Length, Now,
            IsCompressed: true);
        var tools = NewTools(intentId, att, bytes);

        var act = () => tools.ReadIntentAttachmentImage(intentId.Value, "att-big", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentAttachmentTooLarge);
    }

    [Fact(DisplayName = "read_intent_attachment_text c max_chars=10 урезает результат и помечает truncated")]
    public async Task ReadText_caps_by_max_chars()
    {
        var intentId = IntentId.New();
        var text = "abcdefghijklmnopqrstuvwxyz";
        var bytes = Encoding.UTF8.GetBytes(text);
        var att = new IntentAttachment(
            "att-txt", "user-1", intentId.Value, "notes.txt", "text/plain", bytes.Length, Now);

        var tools = NewTools(intentId, att, bytes);

        var slice = await tools.ReadIntentAttachmentText(
            intentId.Value, "att-txt", offset: 0, max_chars: 10, CancellationToken.None);

        slice.Text.Should().Be("abcdefghij");
        slice.ReturnedBytesStart.Should().Be(0);
        slice.ReturnedBytesEnd.Should().Be(10);
        slice.TotalSizeBytes.Should().Be(bytes.Length);
        slice.Truncated.Should().BeTrue();
    }

    [Fact(DisplayName = "read_intent_attachment_text продолжает чтение c offset = returned_bytes_end")]
    public async Task ReadText_continues_with_offset()
    {
        var intentId = IntentId.New();
        var bytes = Encoding.UTF8.GetBytes("HELLO_WORLD");
        var att = new IntentAttachment(
            "att-txt", "user-1", intentId.Value, "f.txt", "text/plain", bytes.Length, Now);
        var tools = NewTools(intentId, att, bytes);

        var slice = await tools.ReadIntentAttachmentText(
            intentId.Value, "att-txt", offset: 6, max_chars: 100, CancellationToken.None);

        slice.Text.Should().Be("WORLD");
        slice.ReturnedBytesStart.Should().Be(6);
        slice.ReturnedBytesEnd.Should().Be(11);
        slice.Truncated.Should().BeFalse();
    }

    [Fact(DisplayName = "read_intent_attachment_text дропает обрывок UTF-8 в начале при offset в середине rune")]
    public async Task ReadText_skips_partial_utf8_at_start()
    {
        var intentId = IntentId.New();
        // "Привет" — каждая буква 2 байта в UTF-8.
        var bytes = Encoding.UTF8.GetBytes("Привет");
        var att = new IntentAttachment(
            "att-ru", "user-1", intentId.Value, "ru.txt", "text/plain", bytes.Length, Now);
        var tools = NewTools(intentId, att, bytes);

        // offset=1 — середина первого rune. Должно дропнуть continuation-байты и начать с "р".
        var slice = await tools.ReadIntentAttachmentText(
            intentId.Value, "att-ru", offset: 1, max_chars: 100, CancellationToken.None);

        slice.Text.Should().Be("ривет");
        slice.ReturnedBytesStart.Should().Be(2);
        slice.Truncated.Should().BeFalse();
    }

    [Fact(DisplayName = "read_intent_attachment_text на image-аттаче бросает validation.failed")]
    public async Task ReadText_on_image_throws_validation()
    {
        var intentId = IntentId.New();
        var att = new IntentAttachment(
            "att-image", "user-1", intentId.Value, "shot.png", "image/png", 4, Now);
        var tools = NewTools(intentId, att, [1, 2, 3, 4]);

        var act = () => tools.ReadIntentAttachmentText(
            intentId.Value, "att-image", offset: 0, max_chars: 50, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Message.Should().Contain("read_intent_attachment_image");
    }

    [Fact(DisplayName = "read_intent_attachment_image на отсутствующем интенте бросает intent.not_found")]
    public async Task ReadImage_intent_missing_throws_not_found()
    {
        var intentId = IntentId.New();

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>()).Returns((Intent?)null);

        var attachmentRepo = Substitute.For<IIntentAttachmentRepository>();
        var tools = new IntentAttachmentTools(intentRepo, attachmentRepo);

        var act = () => tools.ReadIntentAttachmentImage(intentId.Value, "any", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "read_intent_attachment_image на отсутствующем аттаче бросает intent.attachment.not_found")]
    public async Task ReadImage_attachment_missing_throws_not_found()
    {
        var intentId = IntentId.New();

        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "x", IntentStatusNames.Draft, 1, [], Now, Now));

        var attachmentRepo = Substitute.For<IIntentAttachmentRepository>();
        attachmentRepo.OpenContentAsync(Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IntentAttachmentContent?)null);

        var tools = new IntentAttachmentTools(intentRepo, attachmentRepo);

        var act = () => tools.ReadIntentAttachmentImage(intentId.Value, "missing", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentAttachmentNotFound);
    }

    [Fact(DisplayName = "read_intent_attachment_text валидирует max_chars > 200000")]
    public async Task ReadText_rejects_too_large_max_chars()
    {
        var intentId = IntentId.New();
        var att = new IntentAttachment("a", "u", intentId.Value, "f", "text/plain", 0, Now);
        var tools = NewTools(intentId, att, []);

        var act = () => tools.ReadIntentAttachmentText(intentId.Value, "a", 0, 200_001, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static IntentAttachmentTools NewTools(IntentId intentId, IntentAttachment att, byte[] bytes)
    {
        var intentRepo = Substitute.For<IIntentRepository>();
        intentRepo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(Intent.Restore(intentId, "user-1", "body", IntentStatusNames.Draft, 1, [], Now, Now));

        var attachmentRepo = Substitute.For<IIntentAttachmentRepository>();
        attachmentRepo.OpenContentAsync(intentId, att.Id, Arg.Any<CancellationToken>())
            .Returns(_ => new IntentAttachmentContent(att, new MemoryStream(bytes)));

        return new IntentAttachmentTools(intentRepo, attachmentRepo);
    }
}
