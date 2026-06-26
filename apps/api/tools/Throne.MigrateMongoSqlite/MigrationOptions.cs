using Microsoft.Data.Sqlite;

namespace Throne.MigrateMongoSqlite;

internal sealed record MigrationOptions(
    string MongoUri,
    string MongoDatabase,
    string SqliteConnectionString,
    string GridFsBucketName)
{
    public const string Usage =
        "Usage: throne-migrate-mongo-sqlite --mongo-uri <uri> --mongo-database <db> (--sqlite-path <file> | --sqlite-connection <connection-string>) [--gridfs-bucket intent_attachment_fs]";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out MigrationOptions options,
        out string error)
    {
        var values = ParsePairs(args);
        var mongoUri = Value(values, "--mongo-uri");
        var mongoDatabase = Value(values, "--mongo-database");
        var sqlitePath = Value(values, "--sqlite-path");
        var sqliteConnection = Value(values, "--sqlite-connection");
        var bucket = Value(values, "--gridfs-bucket") ?? "intent_attachment_fs";

        options = new MigrationOptions(string.Empty, string.Empty, string.Empty, bucket);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(mongoUri))
        {
            error = "Missing --mongo-uri.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(mongoDatabase))
        {
            error = "Missing --mongo-database.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(sqlitePath) == string.IsNullOrWhiteSpace(sqliteConnection))
        {
            error = "Pass exactly one of --sqlite-path or --sqlite-connection.";
            return false;
        }

        options = new MigrationOptions(
            mongoUri,
            mongoDatabase,
            sqliteConnection ?? BuildSqliteConnectionString(sqlitePath!),
            bucket);
        return true;
    }

    private static Dictionary<string, string?> ParsePairs(IReadOnlyList<string> args)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            result[key] = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[++i]
                : null;
        }
        return result;
    }

    private static string? Value(Dictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string BuildSqliteConnectionString(string path)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path };
        return builder.ToString();
    }
}
