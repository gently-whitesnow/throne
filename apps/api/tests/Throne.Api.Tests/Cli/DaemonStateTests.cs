using FluentAssertions;
using Throne.Api.Cli;

namespace Throne.Api.Tests.Cli;

public class DaemonStateTests : IDisposable
{
    private readonly ThroneHome _home =
        ThroneHome.Resolve(Path.Combine(Path.GetTempPath(), "throne-test-" + Guid.NewGuid().ToString("N")));

    [Fact]
    public void Write_then_read_round_trips_state_with_host_args()
    {
        var state = new DaemonState(1234, "http://localhost:5008", "1.2.3", DateTimeOffset.UtcNow,
            ["--urls", "http://localhost:5008", "--Persistence:Sqlite:DataSource=/x/throne.db"]);
        DaemonState.Write(_home, state);

        File.Exists(_home.PidFile).Should().BeTrue();
        var read = DaemonState.TryRead(_home);

        read.Should().NotBeNull();
        read!.Pid.Should().Be(1234);
        read.Url.Should().Be("http://localhost:5008");
        read.Version.Should().Be("1.2.3");
        read.HostArgs.Should().Equal("--urls", "http://localhost:5008", "--Persistence:Sqlite:DataSource=/x/throne.db");
    }

    [Fact]
    public void Live_process_is_reported_alive_and_dead_one_is_not()
    {
        DaemonState.Write(_home, new DaemonState(
            Environment.ProcessId, "http://localhost:5008", "1.0.0", DateTimeOffset.UtcNow, []));
        DaemonState.TryRead(_home)!.IsAlive.Should().BeTrue();

        DaemonState.Write(_home, new DaemonState(
            int.MaxValue - 1, "http://localhost:5008", "1.0.0", DateTimeOffset.UtcNow, []));
        DaemonState.TryRead(_home)!.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void Clear_removes_both_files()
    {
        DaemonState.Write(_home, new DaemonState(42, "u", "v", DateTimeOffset.UtcNow, []));
        DaemonState.Clear(_home);

        File.Exists(_home.PidFile).Should().BeFalse();
        File.Exists(_home.StateFile).Should().BeFalse();
        DaemonState.TryRead(_home).Should().BeNull();
    }

    [Fact]
    public void Missing_home_reads_as_null()
    {
        DaemonState.TryRead(_home).Should().BeNull();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_home.Directory))
            {
                Directory.Delete(_home.Directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }
}
