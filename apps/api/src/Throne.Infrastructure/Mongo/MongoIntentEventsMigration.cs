using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Throne.Domain.Intents.Events;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// One-shot, idempotent backfill from <c>text_versions</c> (owner_kind=intent) to the
/// new <c>intent_events</c> collection (ADR-0019). Runs once on startup, after the
/// index initializer. Skips per-document if the destination already has a
/// <c>text_changed</c> event for the same <c>(intent_id, version)</c> — the unique
/// partial index on the destination collection backs this up.
/// </summary>
internal sealed partial class MongoIntentEventsMigration(
    IMongoDatabase database,
    ILogger<MongoIntentEventsMigration> logger) : IHostedService
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Information,
        Message = "intent_events migration: copied {Copied}, skipped {Skipped} (already migrated)")]
    private partial void LogMigrationResult(int copied, int skipped);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var textVersions = database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);
        var intentEvents = database.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents);

        var ownerKindWire = TextVersionOwnerKind.Intent.ToWire();
        var filter = Builders<TextVersionDocument>.Filter.Eq(d => d.OwnerKind, ownerKindWire);
        var sort = Builders<TextVersionDocument>.Sort.Ascending(d => d.OwnerId).Ascending(d => d.Version);

        var copied = 0;
        var skipped = 0;

        using var cursor = await textVersions.Find(filter).Sort(sort).ToCursorAsync(cancellationToken);
        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var v in cursor.Current)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var copiedOne = await CopyOneAsync(intentEvents, v, cancellationToken);
                if (copiedOne)
                {
                    copied++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        if (copied > 0 || skipped > 0)
        {
            LogMigrationResult(copied, skipped);
        }
    }

    private static async Task<bool> CopyOneAsync(
        IMongoCollection<IntentEventDocument> intentEvents,
        TextVersionDocument v,
        CancellationToken ct)
    {
        var existing = await intentEvents.Find(
            Builders<IntentEventDocument>.Filter.And(
                Builders<IntentEventDocument>.Filter.Eq(d => d.IntentId, v.OwnerId),
                Builders<IntentEventDocument>.Filter.Eq(d => d.Kind, IntentEventKindWires.TextChanged),
                Builders<IntentEventDocument>.Filter.Eq(d => d.Version, v.Version)))
            .Limit(1)
            .Project(d => d.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return false;
        }

        try
        {
            await intentEvents.InsertOneAsync(ToEventDocument(v), options: null, ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Lost the race to a concurrent writer: the unique partial index
            // already covers `(intent_id, version)` for kind=text_changed.
            return false;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IntentEventDocument ToEventDocument(TextVersionDocument v) => new()
    {
        Id = v.Id,
        IntentId = v.OwnerId,
        PeerIntentId = null,
        Kind = IntentEventKindWires.TextChanged,
        Version = v.Version,
        TextChange = new IntentEventTextChangeSubdocument
        {
            Kind = v.Kind,
            Snapshot = v.Snapshot,
            OldText = v.OldText,
            NewText = v.NewText,
            AfterLine = v.AfterLine,
            InsertText = v.InsertText,
        },
        Link = null,
        CreatedAt = v.ChangedAt,
        CreatedBy = v.ChangedBy,
    };
}
