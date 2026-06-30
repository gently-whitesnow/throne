using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.Tests.Terminals;

public class TmuxSessionManagerNativeViewerTests
{
    [Fact(DisplayName = "PrepareNativeViewer сбрасывает window-size option и включает tmux mouse")]
    public async Task Prepare_native_viewer_resets_size_and_enables_mouse()
    {
        var launcher = Substitute.For<IProcessLauncher>();
        launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProcessRunResult(0, "", "", TimeSpan.Zero)));
        var sut = NewManager(launcher);

        await sut.PrepareNativeViewerAsync("intent-abc", CancellationToken.None);

        var argvs = launcher
            .ReceivedCalls()
            .Select(c => ((ProcessRunRequest)c.GetArguments()[0]!).Arguments)
            .ToArray();

        argvs.Should().HaveCount(2);
        argvs[0].Should().Equal("set-window-option", "-t", "throne-intent-abc", "window-size", "latest");
        argvs[1].Should().Equal("set-option", "-t", "throne-intent-abc", "mouse", "on");
    }

    private static TmuxSessionManager NewManager(IProcessLauncher launcher)
    {
        var cli = new TmuxCli(launcher, Options.Create(new TmuxOptions()));
        return new TmuxSessionManager(
            cli,
            NullLogger<TmuxSessionManager>.Instance,
            Substitute.For<IDomainEventDispatcher>());
    }
}
