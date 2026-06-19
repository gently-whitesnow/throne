using FluentAssertions;
using Throne.Application.Intents;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TerminalAttachmentsContextRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Render возвращает null когда у интента нет аттачей")]
    public void Render_returns_null_when_no_attachments()
    {
        TerminalAttachmentsContextRenderer.Render("intent-1", []).Should().BeNull();
    }

    [Fact(DisplayName = "Render для image-аттача печатает intent_id и нейтральную подсказку без голого имени тула")]
    public void Render_image_attachment_emits_image_hint()
    {
        var att = new IntentAttachment("att-1", "intent-1", "shot.png", "image/png", 12345, Now);

        var text = TerminalAttachmentsContextRenderer.Render("intent-1", [att]);

        text.Should().NotBeNull();
        text!.Split('\n').Should().Equal(
            "[intent attachments]",
            "intent_id=intent-1",
            "- id=att-1 kind=image filename=\"shot.png\" content_type=image/png size_bytes=12345",
            "",
            "To view an image attachment, call your MCP client's attachment-read tool for images "
                + "(the one that loads image bytes as a vision block), passing intent_id and the attachment id above.");
        text.Should().NotContain("read_intent_attachment_image");
    }

    [Fact(DisplayName = "Render для text-аттача печатает intent_id и нейтральную подсказку без голого имени тула")]
    public void Render_text_attachment_emits_text_hint()
    {
        var att = new IntentAttachment("att-1", "intent-1", "trace.log", "text/plain", 1024, Now);

        var text = TerminalAttachmentsContextRenderer.Render("intent-1", [att]);

        text.Should().NotBeNull();
        text!.Should().Contain("intent_id=intent-1").And.Contain("kind=text").And.Contain("text/log files");
        text.Should().NotContain("read_intent_attachment_text").And.NotContain("read_intent_attachment_image");
    }

    [Fact(DisplayName = "Render по смешанным типам печатает обе подсказки в стабильном порядке image -> text")]
    public void Render_mixed_kinds_emits_both_hints()
    {
        var image = new IntentAttachment("img", "intent-1", "shot.png", "image/png", 100, Now);
        var log = new IntentAttachment("log", "intent-1", "trace.log", "text/plain", 200, Now);

        var text = TerminalAttachmentsContextRenderer.Render("intent-1", [image, log]);

        text.Should().NotBeNull();
        var imageHintIdx = text!.IndexOf("image attachment, call", StringComparison.Ordinal);
        var textHintIdx = text.IndexOf("text attachment, call", StringComparison.Ordinal);
        imageHintIdx.Should().BeGreaterThan(0);
        textHintIdx.Should().BeGreaterThan(imageHintIdx);
    }

    [Fact(DisplayName = "Render не подсказывает MCP-тулы для unsupported MIME, но печатает строку аттача")]
    public void Render_unsupported_kind_lists_row_without_hints()
    {
        var att = new IntentAttachment("att-1", "intent-1", "bin.dat", "application/octet-stream", 9, Now);

        var text = TerminalAttachmentsContextRenderer.Render("intent-1", [att]);

        text.Should().NotBeNull();
        text!.Should().Contain("kind=unsupported");
        text.Should().NotContain("attachment-read tool");
    }

    [Fact(DisplayName = "Render экранирует кавычки и обратные слэши в имени файла")]
    public void Render_escapes_quotes_and_backslashes_in_filename()
    {
        var att = new IntentAttachment("att-1", "intent-1", "weird \"name\" \\ path.png", "image/png", 1, Now);

        var text = TerminalAttachmentsContextRenderer.Render("intent-1", [att]);

        text.Should().NotBeNull();
        text!.Should().Contain("filename=\"weird \\\"name\\\" \\\\ path.png\"");
    }
}
