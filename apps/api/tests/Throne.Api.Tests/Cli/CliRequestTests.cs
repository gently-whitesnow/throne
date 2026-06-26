using FluentAssertions;
using Throne.Api.Cli;

namespace Throne.Api.Tests.Cli;

public class CliRequestTests
{
    [Theory]
    [InlineData("stop", "Stop")]
    [InlineData("restart", "Restart")]
    [InlineData("status", "Status")]
    [InlineData("logs", "Logs")]
    [InlineData("update", "Update")]
    [InlineData("serve", "Serve")]
    [InlineData("version", "Version")]
    [InlineData("-v", "Version")]
    [InlineData("--version", "Version")]
    [InlineData("help", "Help")]
    [InlineData("-h", "Help")]
    [InlineData("--help", "Help")]
    public void Maps_verb_to_command(string verb, string expected)
    {
        CliRequest.Parse([verb]).Command.ToString().Should().Be(expected);
    }

    [Fact]
    public void Bare_invocation_is_start()
    {
        var request = CliRequest.Parse([]);

        request.Command.Should().Be(CliCommand.Start);
        request.Attach.Should().BeFalse();
        request.Url.Should().Be(CliRequest.DefaultUrl);
    }

    [Fact]
    public void Attach_and_no_browser_flags_are_lifted_out()
    {
        var request = CliRequest.Parse(["-a", "--no-browser"]);

        request.Command.Should().Be(CliCommand.Start);
        request.Attach.Should().BeTrue();
        request.NoBrowser.Should().BeTrue();
    }

    [Fact]
    public void Logs_follow_flag_is_parsed()
    {
        var request = CliRequest.Parse(["logs", "-f"]);

        request.Command.Should().Be(CliCommand.Logs);
        request.Follow.Should().BeTrue();
    }

    [Fact]
    public void Update_passes_remaining_flags_through_as_rest()
    {
        var request = CliRequest.Parse(["update", "--restart"]);

        request.Command.Should().Be(CliCommand.Update);
        request.Rest.Should().Equal("--restart");
    }

    [Theory]
    [InlineData("8080", false)]
    [InlineData("1", false)]
    [InlineData("65535", false)]
    [InlineData("abc", true)]
    [InlineData("0", true)]
    [InlineData("70000", true)]
    public void Validates_port_alias(string port, bool expectError)
    {
        var error = CliRequest.Parse(["-p", port]).PortError();
        (error is not null).Should().Be(expectError);
    }

    [Fact]
    public void No_port_alias_has_no_port_error()
    {
        CliRequest.Parse([]).PortError().Should().BeNull();
    }
}
