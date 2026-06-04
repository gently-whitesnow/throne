using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class InboundFrameDispatcherTests
{
    [Fact(DisplayName = "Resize-кадр вызывает tmux resize-window для session")]
    public async Task Resize_frame_uses_resize_window()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(0, "", "", TimeSpan.Zero)));
        var tmux = new TmuxCli(launcher, Options.Create(new TmuxOptions()));
        var sut = new InboundFrameDispatcher(tmux);

        await sut.DispatchAsync(new ClientFrame(ClientFrameKind.Resize, null, 120, 40), "throne-intent-1", CancellationToken.None);

        var request = (ProcessRunRequest)launcher.ReceivedCalls().Single().GetArguments()[0]!;
        request.Arguments.Should().Equal("resize-window", "-t", "throne-intent-1", "-x", "120", "-y", "40");
    }

    [Fact(DisplayName = "Input-кадр с ASCII шлёт один hex-байт через send-keys -H")]
    public async Task Ascii_input_sends_single_hex_byte()
    {
        var args = await DispatchInputAsync("A");

        args.Should().Equal("send-keys", "-t", "throne-intent-1", "-H", "41");
    }

    [Fact(DisplayName = "Input-кадр с кириллицей разбивает UTF-8 на отдельные hex-байты")]
    public async Task Cyrillic_input_sends_each_utf8_byte_separately()
    {
        // «д» = U+0434 → UTF-8 D0 B4. Склейка в один аргумент "D0B4" tmux молча
        // отбрасывает, поэтому каждый байт идёт отдельным аргументом.
        var args = await DispatchInputAsync("д");

        args.Should().Equal("send-keys", "-t", "throne-intent-1", "-H", "D0", "B4");
    }

    private static async Task<IReadOnlyList<string>> DispatchInputAsync(string data)
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(0, "", "", TimeSpan.Zero)));
        var tmux = new TmuxCli(launcher, Options.Create(new TmuxOptions()));
        var sut = new InboundFrameDispatcher(tmux);

        await sut.DispatchAsync(new ClientFrame(ClientFrameKind.Input, data, 0, 0), "throne-intent-1", CancellationToken.None);

        var request = (ProcessRunRequest)launcher.ReceivedCalls().Single().GetArguments()[0]!;
        return request.Arguments;
    }
}
