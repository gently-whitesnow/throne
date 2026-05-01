using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentRepository(IMongoDatabase database) : IIntentRepository
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<TextVersionDocument> _textVersions =
        database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);

    public async Task CreateAsync(Intent intent, TextVersion initialVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(initialVersion);

        await _textVersions.InsertOneAsync(MapVersion(initialVersion), options: null, ct).ConfigureAwait(false);
        await _intents.InsertOneAsync(MapIntent(intent), options: null, ct).ConfigureAwait(false);
    }

    public async Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct)
    {
        var document = await _intents
            .Find(d => d.Id == id.Value)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return document is null ? null : MapToDomain(document);
    }

    private static IntentDocument MapIntent(Intent intent) => new()
    {
        Id = intent.Id.Value,
        Text = intent.Text,
        CurrentVersion = intent.CurrentVersion,
        Tags = [.. intent.Tags],
        CreatedAt = intent.CreatedAt.UtcDateTime,
        UpdatedAt = intent.UpdatedAt.UtcDateTime,
    };

    private static TextVersionDocument MapVersion(TextVersion v) => new()
    {
        Id = v.Id,
        OwnerKind = v.OwnerKind.ToWire(),
        OwnerId = v.OwnerId,
        Version = v.Version,
        Kind = v.Kind.ToWire(),
        Snapshot = v.Snapshot,
        OldText = v.OldText,
        NewText = v.NewText,
        AfterLine = v.AfterLine,
        InsertText = v.InsertText,
        ChangedAt = v.ChangedAt.UtcDateTime,
        ChangedBy = v.ChangedBy.ToWire(),
    };

    private static Intent MapToDomain(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        text: doc.Text,
        currentVersion: doc.CurrentVersion,
        tags: doc.Tags,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));
}
