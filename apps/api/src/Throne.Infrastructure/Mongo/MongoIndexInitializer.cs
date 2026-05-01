using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIndexInitializer(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var textVersions = database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);
        await textVersions.Indexes.CreateOneAsync(
            new CreateIndexModel<TextVersionDocument>(
                Builders<TextVersionDocument>.IndexKeys
                    .Ascending(x => x.OwnerKind)
                    .Ascending(x => x.OwnerId)
                    .Ascending(x => x.Version),
                new CreateIndexOptions { Unique = true, Name = "owner_version_unique" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var calls = database.GetCollection<McpCallLogDocument>(MongoCollectionNames.McpCallLog);
        await calls.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "intent_created" }),
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys
                        .Ascending(x => x.SessionId)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "session_created" }),
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys
                        .Ascending(x => x.ToolName)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "tool_created" }),
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys.Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "created_at" }),
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
