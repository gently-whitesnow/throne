using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentRepository(IMongoDatabase database, MongoSessionAccessor sessions) : IIntentRepository
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<TextVersionDocument> _textVersions =
        database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);

    public async Task CreateAsync(Intent intent, TextVersion initialVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(initialVersion);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentRepository.CreateAsync must run inside IUnitOfWork.ExecuteAsync.");

        await _textVersions.InsertOneAsync(session, MapVersion(initialVersion), options: null, ct).ConfigureAwait(false);
        await _intents.InsertOneAsync(session, MapIntent(intent), options: null, ct).ConfigureAwait(false);
    }

    public async Task<ReplaceIntentTextOutcome> ReplaceTextAsync(
        IntentId id,
        int expectedVersion,
        string oldText,
        string newText,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentRepository.ReplaceTextAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (document is null)
        {
            return new ReplaceIntentTextOutcome.NotFound();
        }

        if (document.CurrentVersion != expectedVersion)
        {
            return new ReplaceIntentTextOutcome.VersionConflict(document.CurrentVersion);
        }

        var intent = MapToDomain(document);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = intent.ReplaceText(oldText, newText, newVersionId, now, TextVersionAuthor.Agent);

        switch (domainResult)
        {
            case ReplaceTextResult.MatchNotFound matchNotFound:
                return new ReplaceIntentTextOutcome.MatchNotFound(matchNotFound.QueryPreview);

            case ReplaceTextResult.MatchAmbiguous matchAmbiguous:
                return new ReplaceIntentTextOutcome.MatchAmbiguous(matchAmbiguous.MatchesCount, matchAmbiguous.MatchLines);

            case ReplaceTextResult.Replaced replaced:
                {
                    var update = Builders<IntentDocument>.Update
                        .Set(d => d.Text, intent.Text)
                        .Set(d => d.CurrentVersion, intent.CurrentVersion)
                        .Set(d => d.UpdatedAt, intent.UpdatedAt.UtcDateTime);

                    var updateResult = await _intents.UpdateOneAsync(
                        session,
                        d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
                        update,
                        options: null,
                        ct).ConfigureAwait(false);

                    if (updateResult.ModifiedCount == 0)
                    {
                        var fresh = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                        return new ReplaceIntentTextOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
                    }

                    await _textVersions.InsertOneAsync(session, MapVersion(replaced.Version), options: null, ct).ConfigureAwait(false);
                    return new ReplaceIntentTextOutcome.Replaced(intent);
                }

            default:
                throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}");
        }
    }

    public async Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var document = session is null
            ? await _intents.Find(d => d.Id == id.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false)
            : await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);

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
