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
        TerminalAttachmentsContextRenderer.Render([]).Should().BeNull();
    }

    [Fact(DisplayName = "Render печатает filename и относительный путь без меты")]
    public void Render_emits_filename_and_relative_path_only()
    {
        var att = new IntentAttachment("att-1", "intent-1", "shot.png", "image/png", 12345, Now);

        var text = TerminalAttachmentsContextRenderer.Render([att]);

        text.Should().NotBeNull();
        text!.Split('\n').Should().Equal(
            "[intent attachments]",
            "Files staged in this workspace — open with Read:",
            "- \"shot.png\": .throne/attachments/att-1-shot.png");
        text.Should().NotContain("read_intent_attachment");
        text.Should().NotContain("attachment-read");
        text.Should().NotContain("content_type").And.NotContain("size_bytes").And.NotContain("kind=");
    }

    [Fact(DisplayName = "Render перечисляет и image, и text аттачи их путями в воркспейсе")]
    public void Render_lists_both_image_and_text_attachments()
    {
        var image = new IntentAttachment("img", "intent-1", "shot.png", "image/png", 100, Now);
        var log = new IntentAttachment("log", "intent-1", "trace.log", "text/plain", 200, Now);

        var text = TerminalAttachmentsContextRenderer.Render([image, log]);

        text.Should().NotBeNull();
        text!.Should().Contain("- \"shot.png\": .throne/attachments/img-shot.png");
        text.Should().Contain("- \"trace.log\": .throne/attachments/log-trace.log");
    }

    [Fact(DisplayName = "Render санитизирует разделители пути в имени файла, путь не выходит из .throne/attachments")]
    public void Render_sanitizes_path_separators_in_filename()
    {
        var att = new IntentAttachment("att-1", "intent-1", "../../etc/passwd", "text/plain", 1, Now);

        var text = TerminalAttachmentsContextRenderer.Render([att]);

        text.Should().NotBeNull();
        var pathLine = text!.Split('\n').Single(l => l.Contains(".throne/attachments", StringComparison.Ordinal));
        var path = pathLine.Split(": ", 2)[1];
        path.Should().Be(".throne/attachments/att-1-.._.._etc_passwd");
        path.Should().NotContain("/etc/").And.NotContain("..\\");
    }

    [Fact(DisplayName = "Render экранирует кавычки и обратные слэши в подписи имени файла")]
    public void Render_escapes_quotes_and_backslashes_in_filename_label()
    {
        var att = new IntentAttachment("att-1", "intent-1", "weird \"name\".png", "image/png", 1, Now);

        var text = TerminalAttachmentsContextRenderer.Render([att]);

        text.Should().NotBeNull();
        text!.Should().Contain("- \"weird \\\"name\\\".png\":");
    }
}
