namespace Throne.Api.Cli;

/// <summary>
/// The state directory that defines a single throne instance. One instance per
/// home (default <c>~/.throne</c>); relocating the home — via <c>--home &lt;dir&gt;</c>
/// or <c>THRONE_HOME</c> — isolates the database, pid/state and logs wholesale,
/// the same one-knob pattern as <c>GH_CONFIG_DIR</c>/<c>DOCKER_CONFIG</c>. The pid
/// and log always live under the resolved home; the SQLite and workspace paths only
/// default to it when the home is explicitly relocated, so the unrelocated default
/// keeps the appsettings-driven behaviour untouched.
/// </summary>
internal sealed class ThroneHome
{
    private ThroneHome(string directory, bool isExplicit)
    {
        Directory = directory;
        IsExplicit = isExplicit;
    }

    public string Directory { get; }

    /// <summary>True when the home came from <c>--home</c> or <c>THRONE_HOME</c>.</summary>
    public bool IsExplicit { get; }

    public string PidFile => Path.Combine(Directory, "throne.pid");

    public string StateFile => Path.Combine(Directory, "throne.daemon.json");

    public string LogFile => Path.Combine(Directory, "throne.log");

    public string DbPath => Path.Combine(Directory, "throne.db");

    public string WorkspacesRoot => Path.Combine(Directory, "workspaces");

    public static ThroneHome Resolve(string? overrideDir)
    {
        var explicitDir = !string.IsNullOrWhiteSpace(overrideDir)
            ? overrideDir
            : Environment.GetEnvironmentVariable("THRONE_HOME");

        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            return new ThroneHome(ToAbsolute(explicitDir), isExplicit: true);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new ThroneHome(Path.Combine(home, ".throne"), isExplicit: false);
    }

    public void EnsureDirectory() => System.IO.Directory.CreateDirectory(Directory);

    private static string ToAbsolute(string path)
    {
        var expanded = path;
        if (path == "~")
        {
            expanded = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else if (path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            expanded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        }

        return Path.GetFullPath(expanded);
    }
}
