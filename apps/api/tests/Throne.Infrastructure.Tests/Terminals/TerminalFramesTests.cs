using System.Text.Json;
using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class TerminalFramesTests
{
    [Fact(DisplayName = "EncodeOutput оборачивает данные в JSON output-кадр")]
    public void Encode_output_produces_valid_json()
    {
        var json = TerminalFrames.EncodeOutput("hello [31mworld[0m\n");

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("type").GetString().Should().Be("output");
        doc.RootElement.GetProperty("data").GetString().Should().Be("hello [31mworld[0m\n");
    }
}
