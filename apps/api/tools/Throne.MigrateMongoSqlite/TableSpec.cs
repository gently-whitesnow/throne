using MongoDB.Bson;

namespace Throne.MigrateMongoSqlite;

internal sealed record ColumnValue(string Name, object? Value);

internal sealed record ColumnSpec(string Name, Func<BsonDocument, object?> Read)
{
    public ColumnValue ReadValue(BsonDocument document) => new(Name, Read(document));
}

internal sealed record TableSpec(
    string SourceCollection,
    string TargetTable,
    IReadOnlyList<ColumnSpec> Columns,
    Func<BsonDocument, bool>? Filter = null)
{
    public IReadOnlyList<ColumnValue> ReadValues(BsonDocument document) =>
        Columns.Select(column => column.ReadValue(document)).ToArray();
}
