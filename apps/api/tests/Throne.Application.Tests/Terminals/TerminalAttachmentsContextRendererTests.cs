using FluentAssertions;
using Throne.Application.Intents;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TerminalAttachmentsContextRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Пустой список → null (блок не инжектится)")]
    public void Empty_list_renders_null()
    {
        TerminalAttachmentsContextRenderer.Render(Array.Empty<IntentAttachment>()).Should().BeNull();
    }

    [Fact(DisplayName = "Картинка попадает в блок строкой id/kind=image/filename")]
    public void Single_image_renders_one_line()
    {
        var attachment = NewAttachment("att-1", "screenshot.png", "image/png");

        var rendered = TerminalAttachmentsContextRenderer.Render(new[] { attachment });

        rendered.Should().Be(
            "[intent attachments]\n- id=att-1 kind=image filename=screenshot.png");
    }

    [Fact(DisplayName = "Несколько аттачей разных типов выезжают каждый своей строкой в порядке списка")]
    public void Mixed_attachments_render_each_line()
    {
        var attachments = new[]
        {
            NewAttachment("att-1", "shot.png", "image/png"),
            NewAttachment("att-2", "trace.log", "text/plain"),
            NewAttachment("att-3", "blob.bin", "application/octet-stream"),
        };

        var rendered = TerminalAttachmentsContextRenderer.Render(attachments);

        rendered.Should().Be(
            "[intent attachments]\n"
            + "- id=att-1 kind=image filename=shot.png\n"
            + "- id=att-2 kind=text filename=trace.log\n"
            + "- id=att-3 kind=unsupported filename=blob.bin");
    }

    private static IntentAttachment NewAttachment(string id, string fileName, string contentType) =>
        new(id, "intent-1", fileName, contentType, SizeBytes: 100, CreatedAt: Now);
}
