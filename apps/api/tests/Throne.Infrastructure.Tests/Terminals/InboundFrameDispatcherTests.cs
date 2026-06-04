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
}
