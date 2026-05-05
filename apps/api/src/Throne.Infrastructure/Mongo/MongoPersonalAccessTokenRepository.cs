using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Domain.Auth;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoPersonalAccessTokenRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    ICurrentUserAccessor currentUser) : IPersonalAccessTokenRepository
{
    private readonly IMongoCollection<PersonalAccessTokenDocument> _collection =
        database.GetCollection<PersonalAccessTokenDocument>(MongoCollectionNames.PersonalAccessTokens);

    public async Task ReplaceForOwnerAsync(PersonalAccessToken token, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (!string.Equals(token.OwnerUserId, currentUser.UserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "PersonalAccessToken.OwnerUserId must match the current user.");
        }

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoPersonalAccessTokenRepository.ReplaceForOwnerAsync must run inside IUnitOfWork.ExecuteAsync.");

        var ownerFilter = Builders<PersonalAccessTokenDocument>.Filter
            .Eq(d => d.OwnerUserId, token.OwnerUserId);

        await _collection.DeleteManyAsync(session, ownerFilter, options: null, ct).ConfigureAwait(false);
        await _collection.InsertOneAsync(session, Map(token), options: null, ct).ConfigureAwait(false);
    }

    public async Task<PersonalAccessToken?> FindByOwnerAsync(string ownerUserId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);

        var session = sessions.Current;
        var filter = Builders<PersonalAccessTokenDocument>.Filter.Eq(d => d.OwnerUserId, ownerUserId);

        var doc = session is null
            ? await _collection.Find(filter).FirstOrDefaultAsync(ct).ConfigureAwait(false)
            : await _collection.Find(session, filter).FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return doc is null ? null : MapToDomain(doc);
    }

    public async Task<PersonalAccessToken?> FindByHashAsync(string hashSha256, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSha256);

        // Resolver работает до аутентификации, поэтому намеренно не использует
        // OwnerUserId-фильтр (фильтрация выполнена ровно в этом запросе через unique-hash).
        var filter = Builders<PersonalAccessTokenDocument>.Filter.Eq(d => d.HashSha256, hashSha256);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return doc is null ? null : MapToDomain(doc);
    }

    private static PersonalAccessTokenDocument Map(PersonalAccessToken token) => new()
    {
        Id = token.Id,
        OwnerUserId = token.OwnerUserId,
        HashSha256 = token.HashSha256,
        LastFour = token.LastFour,
        CreatedAt = token.CreatedAt.UtcDateTime,
    };

    private static PersonalAccessToken MapToDomain(PersonalAccessTokenDocument doc) =>
        PersonalAccessToken.Restore(
            id: doc.Id,
            ownerUserId: doc.OwnerUserId,
            hashSha256: doc.HashSha256,
            lastFour: doc.LastFour,
            createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc));
}
