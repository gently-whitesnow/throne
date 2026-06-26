using FluentAssertions;
using Microsoft.Data.Sqlite;
using MongoDB.Bson;
using Throne.MigrateMongoSqlite;

namespace Throne.MigrateMongoSqlite.Tests;

public sealed class SqliteWriterTests
{
    [Fact]
    public async Task Insert_writes_attachment_blob_content()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateAttachmentTableAsync(connection);

        var writer = new SqliteWriter(connection);
        await writer.EnsureSchemaAsync([AttachmentRows.TargetTable], CancellationToken.None);
        await writer.EnsureEmptyAsync([AttachmentRows.TargetTable], CancellationToken.None);
        using var transaction = connection.BeginTransaction();

        var metadata = new BsonDocument
        {
            ["_id"] = "attachment-1",
            ["intent_id"] = "intent-1",
            ["file_name"] = "proof.png",
            ["content_type"] = "image/png",
            ["size_bytes"] = 3L,
            ["created_at"] = new DateTime(2026, 6, 26, 13, 0, 0, DateTimeKind.Utc),
        };

        await writer.InsertAsync(
            AttachmentRows.TargetTable,
            AttachmentRows.ReadValues(metadata, [1, 2, 255]),
            transaction,
            CancellationToken.None);
        transaction.Commit();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT file_name, size_bytes, hex(content_bytes) FROM intent_attachments;";
        await using var reader = await command.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetString(0).Should().Be("proof.png");
        reader.GetInt64(1).Should().Be(3L);
        reader.GetString(2).Should().Be("0102FF");
    }

    private static async Task CreateAttachmentTableAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE intent_attachments (
                id TEXT NOT NULL PRIMARY KEY,
                intent_id TEXT NOT NULL,
                file_name TEXT NOT NULL,
                content_type TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                compression_state TEXT NULL,
                derived_width INTEGER NULL,
                derived_height INTEGER NULL,
                content_bytes BLOB NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync();
    }
}
