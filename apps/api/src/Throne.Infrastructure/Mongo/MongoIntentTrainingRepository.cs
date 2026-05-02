using MongoDB.Driver;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentTrainingRepository(IMongoDatabase database, MongoSessionAccessor sessions)
    : IIntentTrainingRepository
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<IntentQaDocument> _qa =
        database.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa);

    private readonly IMongoCollection<IntentReviewDocument> _reviews =
        database.GetCollection<IntentReviewDocument>(MongoCollectionNames.IntentReview);

    public async Task<AppendTrainingOutcome> AddQaAsync(
        IntentId id,
        int expectedVersion,
        IntentQa qa,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qa);

        var session = RequireSession(nameof(AddQaAsync));

        var bumpOutcome = await CheckVersionAndBumpUpdatedAtAsync(
            session, id, expectedVersion, now, () => new IntentQaAdded(qa), ct).ConfigureAwait(false);
        if (bumpOutcome is not AppendTrainingOutcome.Appended appended)
        {
            return bumpOutcome;
        }

        await _qa.InsertOneAsync(session, MapQa(qa), options: null, ct).ConfigureAwait(false);
        return appended;
    }

    public async Task<AppendTrainingOutcome> AddReviewAsync(
        IntentId id,
        int expectedVersion,
        IntentReview review,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(review);

        var session = RequireSession(nameof(AddReviewAsync));

        var bumpOutcome = await CheckVersionAndBumpUpdatedAtAsync(
            session, id, expectedVersion, now, () => new IntentReviewAdded(review), ct).ConfigureAwait(false);
        if (bumpOutcome is not AppendTrainingOutcome.Appended appended)
        {
            return bumpOutcome;
        }

        await _reviews.InsertOneAsync(session, MapReview(review), options: null, ct).ConfigureAwait(false);
        return appended;
    }

    public async Task<IReadOnlyList<IntentQa>> ListQaByIntentAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = Builders<IntentQaDocument>.Filter.Eq(d => d.IntentId, id.Value);

        var documents = session is null
            ? await _qa.Find(filter).SortBy(d => d.CreatedAt).ThenBy(d => d.Id).ToListAsync(ct).ConfigureAwait(false)
            : await _qa.Find(session, filter).SortBy(d => d.CreatedAt).ThenBy(d => d.Id).ToListAsync(ct).ConfigureAwait(false);

        var result = new List<IntentQa>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(MapQaToDomain(doc));
        }
        return result;
    }

    public async Task<IReadOnlyList<IntentReview>> ListReviewsByIntentAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = Builders<IntentReviewDocument>.Filter.Eq(d => d.IntentId, id.Value);

        var documents = session is null
            ? await _reviews.Find(filter).SortBy(d => d.CreatedAt).ThenBy(d => d.Id).ToListAsync(ct).ConfigureAwait(false)
            : await _reviews.Find(session, filter).SortBy(d => d.CreatedAt).ThenBy(d => d.Id).ToListAsync(ct).ConfigureAwait(false);

        var result = new List<IntentReview>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(MapReviewToDomain(doc));
        }
        return result;
    }

    private MongoDB.Driver.IClientSessionHandle RequireSession(string method) =>
        sessions.Current
        ?? throw new InvalidOperationException(
            $"MongoIntentTrainingRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private async Task<AppendTrainingOutcome> CheckVersionAndBumpUpdatedAtAsync(
        IClientSessionHandle session,
        IntentId id,
        int expectedVersion,
        DateTimeOffset now,
        Func<IDomainEvent> eventFactory,
        CancellationToken ct)
    {
        var update = Builders<IntentDocument>.Update.Set(d => d.UpdatedAt, now.UtcDateTime);
        var updateResult = await _intents.UpdateOneAsync(
            session,
            d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
            update,
            options: null,
            ct).ConfigureAwait(false);

        if (updateResult.ModifiedCount > 0 || updateResult.MatchedCount > 0)
        {
            return new AppendTrainingOutcome.Appended(expectedVersion, eventFactory());
        }

        var fresh = await _intents.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (fresh is null)
        {
            return new AppendTrainingOutcome.NotFound();
        }

        return new AppendTrainingOutcome.VersionConflict(fresh.CurrentVersion);
    }

    private static IntentQaDocument MapQa(IntentQa qa) => new()
    {
        Id = qa.Id,
        IntentId = qa.IntentId.Value,
        IntentVersionAtWrite = qa.IntentVersionAtWrite,
        Question = qa.Question,
        Answer = qa.Answer,
        CreatedAt = qa.CreatedAt.UtcDateTime,
        CreatedBy = qa.CreatedBy.ToWire(),
    };

    private static IntentReviewDocument MapReview(IntentReview r) => new()
    {
        Id = r.Id,
        IntentId = r.IntentId.Value,
        IntentVersionAtWrite = r.IntentVersionAtWrite,
        Note = r.Note,
        Reason = r.Reason,
        CreatedAt = r.CreatedAt.UtcDateTime,
        CreatedBy = r.CreatedBy.ToWire(),
    };

    private static IntentQa MapQaToDomain(IntentQaDocument doc) => new(
        Id: doc.Id,
        IntentId: new IntentId(doc.IntentId),
        IntentVersionAtWrite: doc.IntentVersionAtWrite,
        Question: doc.Question,
        Answer: doc.Answer,
        CreatedAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        CreatedBy: MongoEnumNames.ParseIntentTrainingAuthor(doc.CreatedBy));

    private static IntentReview MapReviewToDomain(IntentReviewDocument doc) => new(
        Id: doc.Id,
        IntentId: new IntentId(doc.IntentId),
        IntentVersionAtWrite: doc.IntentVersionAtWrite,
        Note: doc.Note,
        Reason: doc.Reason,
        CreatedAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        CreatedBy: MongoEnumNames.ParseIntentTrainingAuthor(doc.CreatedBy));
}
