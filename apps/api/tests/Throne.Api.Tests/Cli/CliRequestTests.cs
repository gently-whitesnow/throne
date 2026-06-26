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
    public void Port_alias_becomes_url_and_host_urls_arg()
    {
        var request = CliRequest.Parse(["-p", "9000"]);

        request.Url.Should().Be("http://localhost:9000");
        request.HostArgs.Should().ContainInOrder("--urls", "http://localhost:9000");
    }

    [Fact]
    public void Db_alias_lowers_onto_persistence_config_key()
    {
        var request = CliRequest.Parse(["--db", "/data/custom.db"]);

        request.HostArgs.Should().Contain("--Persistence:Sqlite:DataSource=/data/custom.db");
    }

    [Fact]
    public void Explicit_home_defaults_db_and_workspace_under_home()
    {
        var request = CliRequest.Parse(["--home", "/srv/throne-x"]);

        request.Home.IsExplicit.Should().BeTrue();
        request.HostArgs.Should().Contain(a => a.Contains("Persistence:Sqlite:DataSource=", StringComparison.Ordinal)
            && a.Contains("throne-x", StringComparison.Ordinal) && a.EndsWith("throne.db", StringComparison.Ordinal));
        request.HostArgs.Should().Contain(a => a.Contains("Throne:Workspace:Root=", StringComparison.Ordinal)
            && a.EndsWith("workspaces", StringComparison.Ordinal));
    }

    [Fact]
    public void Default_home_does_not_override_persistence_or_workspace()
    {
        var request = CliRequest.Parse(["-a"]);

        request.HostArgs.Should().NotContain(a => a.Contains("Persistence:Sqlite:DataSource=", StringComparison.Ordinal));
        request.HostArgs.Should().NotContain(a => a.Contains("Throne:Workspace:Root=", StringComparison.Ordinal));
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

    [Fact]
    public void Serve_keeps_passthrough_host_args_and_normalizes_url()
    {
        var request = CliRequest.Parse(["serve", "--urls", "http://0.0.0.0:7000"]);

        request.Command.Should().Be(CliCommand.Serve);
        request.Url.Should().Be("http://localhost:7000");
        request.HostArgs.Should().ContainInOrder("--urls", "http://0.0.0.0:7000");
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
