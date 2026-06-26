using Microsoft.Data.Sqlite;

namespace Throne.MigrateMongoSqlite;

internal sealed class SqliteWriter(SqliteConnection connection)
{
    public async Task EnsureSchemaAsync(IEnumerable<string> tables, CancellationToken ct)
    {
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
            command.Parameters.AddWithValue("$name", table);
            var exists = await command.ExecuteScalarAsync(ct);
            if (exists is null)
            {
                throw new InvalidOperationException($"SQLite table '{table}' does not exist.");
            }
        }
    }

    public async Task EnsureEmptyAsync(IEnumerable<string> tables, CancellationToken ct)
    {
        foreach (var table in tables)
        {
            var count = await CountAsync(table, ct);
            if (count != 0)
            {
                throw new InvalidOperationException(
                    $"SQLite target table '{table}' is not empty ({count} rows).");
            }
        }
    }

    public async Task InsertAsync(
        string table,
        IReadOnlyList<ColumnValue> values,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var columns = string.Join(", ", values.Select(value => Quote(value.Name)));
        var parameters = string.Join(", ", values.Select((_, index) => $"$p{index}"));
        command.CommandText = $"INSERT INTO {Quote(table)} ({columns}) VALUES ({parameters});";

        for (var i = 0; i < values.Count; i++)
        {
            AddParameter(command, $"$p{i}", values[i].Value);
        }

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<long> CountAsync(string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {Quote(table)};";
        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        if (value is byte[] bytes)
        {
            var parameter = command.Parameters.Add(name, SqliteType.Blob);
            parameter.Value = bytes;
            return;
        }

        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static string Quote(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}
