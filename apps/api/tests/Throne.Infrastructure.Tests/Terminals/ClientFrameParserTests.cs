using FluentAssertions;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Covers the wire-contract surface declared in
/// <c>specs/contracts/realtime/websocket/terminal.yaml</c> for inbound frames.
/// Both happy-path frames + the various malformed shapes a buggy client could send.
/// </summary>
public class ClientFrameParserTests
{
    [Fact(DisplayName = "Парсит input-кадр с произвольной UTF-8 строкой")]
    public void Parses_input_frame()
    {
        var ok = ClientFrameParser.TryParse(@"{""type"":""input"",""data"":""ls -la\n""}", out var frame);

        ok.Should().BeTrue();
        frame.Kind.Should().Be(ClientFrameKind.Input);
        frame.Data.Should().Be("ls -la\n");
    }

    [Fact(DisplayName = "Парсит resize-кадр с валидными размерами")]
    public void Parses_resize_frame()
    {
        var ok = ClientFrameParser.TryParse(@"{""type"":""resize"",""cols"":120,""rows"":40}", out var frame);

        ok.Should().BeTrue();
        frame.Kind.Should().Be(ClientFrameKind.Resize);
        frame.Cols.Should().Be(120);
        frame.Rows.Should().Be(40);
    }

    [Theory(DisplayName = "Отбрасывает невалидные кадры")]
    [InlineData(@"{""type"":""unknown""}")]
    [InlineData(@"{""type"":""input""}")]
    [InlineData(@"{""type"":""resize"",""cols"":120}")]
    [InlineData(@"{""type"":""resize"",""cols"":0,""rows"":40}")]
    [InlineData(@"{""type"":""resize"",""cols"":120,""rows"":99999}")]
    [InlineData(@"not-json")]
    [InlineData(@"[]")]
    [InlineData("")]
    public void Rejects_invalid_payload(string payload)
    {
        ClientFrameParser.TryParse(payload, out _).Should().BeFalse();
    }
}
