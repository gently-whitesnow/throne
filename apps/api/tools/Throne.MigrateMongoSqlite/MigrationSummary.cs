using System.Text;

namespace Throne.MigrateMongoSqlite;

internal sealed class MigrationSummary
{
    private readonly SortedDictionary<string, int> _rows = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Rows => _rows;

    public void Add(string table, int count) => _rows[table] = count;

    public string Format()
    {
        var builder = new StringBuilder("Mongo -> SQLite migration completed.");
        foreach (var (table, count) in _rows)
        {
            builder.AppendLine();
            builder.Append(table).Append(": ").Append(count);
        }
        return builder.ToString();
    }
}
