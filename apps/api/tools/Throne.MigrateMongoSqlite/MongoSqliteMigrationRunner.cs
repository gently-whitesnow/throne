using Microsoft.Data.Sqlite;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Throne.MigrateMongoSqlite;

internal sealed class MongoSqliteMigrationRunner
{
    public static async Task<MigrationSummary> RunAsync(MigrationOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);

        var client = new MongoClient(options.MongoUri);
        var database = client.GetDatabase(options.MongoDatabase);
        await using var connection = new SqliteConnection(options.SqliteConnectionString);
        await connection.OpenAsync(ct);

        var writer = new SqliteWriter(connection);
        await writer.EnsureSchemaAsync(TableSpecs.TargetTables, ct);
        await writer.EnsureEmptyAsync(TableSpecs.TargetTables, ct);

        using var transaction = connection.BeginTransaction();
        var summary = new MigrationSummary();
        foreach (var table in TableSpecs.DocumentTables)
        {
            summary.Add(table.TargetTable, await CopyTableAsync(database, writer, transaction, table, ct));
        }

        var attachments = new AttachmentMigrator(database, options.GridFsBucketName);
        summary.Add(
            AttachmentRows.TargetTable,
            await attachments.CopyAsync(writer, transaction, ct));

        transaction.Commit();
        return summary;
    }

    private static async Task<int> CopyTableAsync(
        IMongoDatabase database,
        SqliteWriter writer,
        SqliteTransaction transaction,
        TableSpec table,
        CancellationToken ct)
    {
        var count = 0;
        var collection = database.GetCollection<BsonDocument>(table.SourceCollection);
        using var cursor = await collection.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync(ct);
        while (await cursor.MoveNextAsync(ct))
        {
            foreach (var document in cursor.Current)
            {
                if (table.Filter is not null && !table.Filter(document))
                {
                    continue;
                }

                await writer.InsertAsync(table.TargetTable, table.ReadValues(document), transaction, ct);
                count++;
            }
        }
        return count;
    }
}
