namespace Throne.Api.Cli;

/// <summary>Human-readable <c>--help</c> and <c>version</c> output.</summary>
internal static class CliHelp
{
    private const string Text = """
        throne — local cockpit (UI + API + SQLite) in a single process.

        Usage:
          throne                 start detached, open the browser, print the URL
          throne -a, --attach    start in the foreground (logs to console, Ctrl-C stops)
          throne stop            stop the running instance
          throne restart         stop, then start detached
          throne status          running state, port, pid, version, home
          throne logs [-f]        tail the daemon log (-f / --follow to stream)
          throne update [--restart]   self-update from the latest GitHub release
          throne version, -v     print the version
          throne --help, -h      show this help

        Options (apply to start):
          -p, --port <n>         port alias (over Urls / --urls / ASPNETCORE_URLS)
          --db <path>            SQLite path alias (over Persistence:Sqlite:DataSource)
          --home <dir>           relocate the whole state dir (db, pid, logs); env THRONE_HOME
          --no-browser           never open the browser (env THRONE_NO_BROWSER)

        One instance per home (default ~/.throne). Relocate the home to run an
        isolated instance, e.g.:  THRONE_HOME=./.throne-agent throne -p 5009
        """;

    public static int Print()
    {
        Console.WriteLine(Text);
        return 0;
    }

    public static int PrintVersion()
    {
        Console.WriteLine(ThroneVersion.Current);
        return 0;
    }
}
