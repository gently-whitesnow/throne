using FluentAssertions;
using Throne.Api.Cli;

namespace Throne.Api.Tests.Cli;

public class ThroneHomeTests
{
    [Fact]
    public void Explicit_override_is_absolute_and_marked_explicit()
    {
        var home = ThroneHome.Resolve("/srv/throne-x");

        home.IsExplicit.Should().BeTrue();
        home.Directory.Should().Be(Path.GetFullPath("/srv/throne-x"));
        home.PidFile.Should().Be(Path.Combine(home.Directory, "throne.pid"));
        home.StateFile.Should().Be(Path.Combine(home.Directory, "throne.daemon.json"));
        home.LogFile.Should().Be(Path.Combine(home.Directory, "throne.log"));
        home.DbPath.Should().Be(Path.Combine(home.Directory, "throne.db"));
        home.WorkspacesRoot.Should().Be(Path.Combine(home.Directory, "workspaces"));
    }

    [Fact]
    public void Relative_override_is_resolved_against_cwd()
    {
        var home = ThroneHome.Resolve("./.throne-agent");

        Path.IsPathRooted(home.Directory).Should().BeTrue();
        home.Directory.Should().EndWith(".throne-agent");
    }

    [Fact]
    public void Default_home_is_under_user_profile_and_not_explicit()
    {
        var previous = Environment.GetEnvironmentVariable("THRONE_HOME");
        Environment.SetEnvironmentVariable("THRONE_HOME", null);
        try
        {
            var home = ThroneHome.Resolve(null);

            home.IsExplicit.Should().BeFalse();
            home.Directory.Should().Be(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".throne"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("THRONE_HOME", previous);
        }
    }
}
