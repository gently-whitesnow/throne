using System.Globalization;

namespace Throne.Api.Cli;

/// <summary>
/// One parsed invocation of the <c>throne</c> CLI. Global flags
/// (<c>--home</c>/<c>-p</c>/<c>--db</c>/<c>-a</c>/<c>--no-browser</c>/<c>-f</c>) are
/// human aliases lifted out of the raw args; <see cref="HostArgs"/> is what the
/// Kestrel host actually receives, with the port/db/home aliases lowered onto the
/// existing ASP.NET config keys (<c>--urls</c> and <c>Throne:ApiBaseUrl</c>,
/// <c>Persistence:Sqlite:DataSource</c>, <c>Throne:Workspace:Root</c>). Command-line
/// config wins over env/appsettings, so
/// an explicit <c>--urls</c> the user passes is overridden only when they also pass
/// <c>-p</c>.
/// </summary>
internal sealed record CliRequest(
    CliCommand Command,
    ThroneHome Home,
    bool Attach,
    bool NoBrowser,
    bool Follow,
    string? Port,
    string Url,
    string[] HostArgs,
    string[] Rest)
{
    public const string DefaultUrl = "http://localhost:5008";

    public static CliRequest Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? homeOverride = null;
        string? port = null;
        string? db = null;
        var attach = false;
        var noBrowser = false;
        var follow = false;
        var rest = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--home":
                    homeOverride = Next(args, ref i);
                    break;
                case "-p" or "--port":
                    port = Next(args, ref i);
                    break;
                case "--db":
                    db = Next(args, ref i);
                    break;
                case "-a" or "--attach":
                    attach = true;
                    break;
                case "--no-browser":
                    noBrowser = true;
                    break;
                case "-f" or "--follow":
                    follow = true;
                    break;
                default:
                    rest.Add(args[i]);
                    break;
            }
        }

        var command = MapCommand(rest.Count > 0 ? rest[0] : null);
        var passthrough = command == CliCommand.Start ? rest.ToArray() : rest.Skip(1).ToArray();
        var home = ThroneHome.Resolve(homeOverride);

        return new CliRequest(
            command,
            home,
            attach,
            noBrowser,
            follow,
            port,
            ResolveUrl(port, passthrough),
            BuildHostArgs(passthrough, port, db, home),
            passthrough);
    }

    /// <summary>Consumes the value token following a value-flag; null if the flag is dangling.</summary>
    private static string? Next(string[] args, ref int i) => i + 1 < args.Length ? args[++i] : null;

    private static CliCommand MapCommand(string? verb) => verb switch
    {
        "stop" => CliCommand.Stop,
        "restart" => CliCommand.Restart,
        "status" => CliCommand.Status,
        "logs" => CliCommand.Logs,
        "update" => CliCommand.Update,
        "serve" => CliCommand.Serve,
        "version" or "-v" or "--version" => CliCommand.Version,
        "help" or "-h" or "--help" => CliCommand.Help,
        _ => CliCommand.Start,
    };

    private static string ResolveUrl(string? port, string[] passthrough)
    {
        if (port is not null)
        {
            return $"http://localhost:{port}";
        }

        for (var i = 0; i < passthrough.Length; i++)
        {
            if (passthrough[i] == "--urls" && i + 1 < passthrough.Length)
            {
                return NormalizeForBrowser(passthrough[i + 1]);
            }

            if (passthrough[i].StartsWith("--urls=", StringComparison.Ordinal))
            {
                return NormalizeForBrowser(passthrough[i]["--urls=".Length..]);
            }
        }

        return DefaultUrl;
    }

    /// <summary>First bound URL with wildcard hosts swapped to localhost so it is clickable.</summary>
    private static string NormalizeForBrowser(string urls)
    {
        var first = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? DefaultUrl;
        return first
            .Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal)
            .Replace("://+", "://localhost", StringComparison.Ordinal)
            .Replace("://*", "://localhost", StringComparison.Ordinal);
    }

    // Idempotent: a home-default override is only appended when the key is not already
    // present. This matters for the daemon child, which re-parses already-resolved args
    // (with THRONE_HOME set ⇒ home is explicit) and must not clobber an explicit
    // `--db`/`--urls` the parent already lowered onto the config keys.
    private static string[] BuildHostArgs(string[] passthrough, string? port, string? db, ThroneHome home)
    {
        const string dataSourceArg = "--Persistence:Sqlite:DataSource=";
        const string workspaceArg = "--Throne:Workspace:Root=";
        var list = new List<string>(passthrough);

        if (port is not null)
        {
            list.Add("--urls");
            list.Add($"http://localhost:{port.Trim()}");
            // -p is the single source of truth for the port: keep the hook/spawn callback
            // URL (Throne:ApiBaseUrl) in sync with --urls so an ephemeral instance's hooks
            // reach it instead of leaking to the default :5008.
            list.Add($"--Throne:ApiBaseUrl=http://localhost:{port.Trim()}");
        }

        if (db is not null)
        {
            list.Add(FormattableString.Invariant($"{dataSourceArg}{db}"));
        }
        else if (home.IsExplicit && !Has(passthrough, dataSourceArg))
        {
            list.Add(FormattableString.Invariant($"{dataSourceArg}{home.DbPath}"));
        }

        if (home.IsExplicit && !Has(passthrough, workspaceArg))
        {
            list.Add(FormattableString.Invariant($"{workspaceArg}{home.WorkspacesRoot}"));
        }

        return list.ToArray();
    }

    private static bool Has(string[] passthrough, string argPrefix) =>
        Array.Exists(passthrough, a => a.StartsWith(argPrefix, StringComparison.Ordinal));

    /// <summary>Validates the <c>-p</c>/<c>--port</c> alias when present; null when valid.</summary>
    public string? PortError()
    {
        if (Port is null)
        {
            return null;
        }

        return int.TryParse(Port.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var p) && p is >= 1 and <= 65535
            ? null
            : $"invalid port '{Port}' — expected 1..65535.";
    }
}
