using FluentAssertions;
using Throne.Api.Cli;

namespace Throne.Api.Tests.Cli;

public class RestartRelaunchTests
{
    [Fact]
    public void Replays_persisted_host_args_and_url()
    {
        var request = CliRequest.Parse(["restart"]);
        var previous = new DaemonState(99, "http://localhost:5009", "1.0.0", DateTimeOffset.UtcNow,
            ["--urls", "http://localhost:5009", "--Persistence:Sqlite:DataSource=/x/throne.db"]);

        var relaunch = RestartCommand.Relaunch(request, previous);

        relaunch.Attach.Should().BeFalse();
        relaunch.Url.Should().Be("http://localhost:5009");
        relaunch.HostArgs.Should().Equal(
            "--urls", "http://localhost:5009", "--Persistence:Sqlite:DataSource=/x/throne.db");
    }

    [Fact]
    public void Explicit_port_overrides_replayed_url_last()
    {
        var request = CliRequest.Parse(["-p", "6000", "restart"]);
        var previous = new DaemonState(99, "http://localhost:5009", "1.0.0", DateTimeOffset.UtcNow,
            ["--urls", "http://localhost:5009"]);

        var relaunch = RestartCommand.Relaunch(request, previous);

        relaunch.Url.Should().Be("http://localhost:6000");
        relaunch.HostArgs.Should().ContainInOrder("--urls", "http://localhost:5009", "--urls", "http://localhost:6000");
        relaunch.HostArgs[^1].Should().Be("http://localhost:6000");
    }

    [Fact]
    public void Falls_back_to_invocation_args_when_no_state()
    {
        var request = CliRequest.Parse(["-p", "7000", "restart"]);

        var relaunch = RestartCommand.Relaunch(request, previous: null);

        relaunch.Attach.Should().BeFalse();
        relaunch.HostArgs.Should().ContainInOrder("--urls", "http://localhost:7000");
    }
}
