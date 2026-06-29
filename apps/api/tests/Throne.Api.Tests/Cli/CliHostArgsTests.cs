using FluentAssertions;
using Throne.Api.Cli;

namespace Throne.Api.Tests.Cli;

public class CliHostArgsTests
{
    [Fact]
    public void Port_alias_becomes_url_and_host_urls_arg()
    {
        var request = CliRequest.Parse(["-p", "9000"]);

        request.Url.Should().Be("http://localhost:9000");
        request.HostArgs.Should().ContainInOrder("--urls", "http://localhost:9000");
    }

    [Fact]
    public void Port_alias_also_lowers_onto_api_base_url_so_hooks_reach_the_instance()
    {
        var request = CliRequest.Parse(["-p", "9000"]);

        request.HostArgs.Should().Contain("--Throne:ApiBaseUrl=http://localhost:9000");
    }

    [Fact]
    public void Without_port_api_base_url_is_left_at_its_default()
    {
        var request = CliRequest.Parse(["-a"]);

        request.HostArgs.Should().NotContain(a => a.StartsWith("--Throne:ApiBaseUrl=", StringComparison.Ordinal));
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
    public void Serve_keeps_passthrough_host_args_and_normalizes_url()
    {
        var request = CliRequest.Parse(["serve", "--urls", "http://0.0.0.0:7000"]);

        request.Command.Should().Be(CliCommand.Serve);
        request.Url.Should().Be("http://localhost:7000");
        request.HostArgs.Should().ContainInOrder("--urls", "http://0.0.0.0:7000");
    }

    [Fact]
    public void Re_parsing_resolved_serve_args_does_not_clobber_custom_db()
    {
        // The daemon child re-parses the parent's already-resolved args with THRONE_HOME
        // (⇒ home explicit). It must keep the custom DataSource, not re-append the home default.
        var request = CliRequest.Parse([
            "--home", "/srv/h", "serve",
            "--urls", "http://localhost:5099",
            "--Persistence:Sqlite:DataSource=/data/custom.db",
            "--Throne:Workspace:Root=/srv/h/workspaces",
        ]);

        request.HostArgs.Count(a => a.StartsWith("--Persistence:Sqlite:DataSource=", StringComparison.Ordinal))
            .Should().Be(1);
        request.HostArgs.Should().Contain("--Persistence:Sqlite:DataSource=/data/custom.db");
        request.HostArgs.Should().NotContain(a => a.EndsWith("/srv/h/throne.db", StringComparison.Ordinal));
    }
}
