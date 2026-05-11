using System.Globalization;
using System.Text;
using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Application.InstructionPatches;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Mongo persistence for <see cref="InstructionPatch"/>. Owner filter is
/// applied on every read and on every state-changing update — the unit-tests
/// in <c>MongoOwnerUserIdIsolationTests</c> assert it.
/// </summary>
internal sealed class MongoInstructionPatchRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    ICurrentUserAccessor currentUser) : IInstructionPatchRepository
{
    private readonly IMongoCollection<InstructionPatchDocument> _collection =
        database.GetCollection<InstructionPatchDocument>(MongoCollectionNames.InstructionPatches);

    public async Task<CreateInstructionPatchOutcome> CreateAsync(
        InstructionPatch patch,
        string? idempotencyKey,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var session = sessions.Current;
        var doc = ToDocument(patch);
        doc.IdempotencyKey = idempotencyKey;
        try
        {
            if (session is null)
            {
                await _collection.InsertOneAsync(doc, options: null, ct);
            }
            else
            {
                await _collection.InsertOneAsync(session, doc, options: null, ct);
            }
            return new CreateInstructionPatchOutcome(patch);
        }
        catch (MongoWriteException ex)
            when (idempotencyKey is not null
                  && ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey, useSession: false, ct);
            if (existing is null)
            {
                throw;
            }
            return new CreateInstructionPatchOutcome(existing, IsExisting: true);
        }
    }

    public Task<InstructionPatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return FindByIdempotencyKeyAsync(idempotencyKey, useSession: true, ct);
    }

    private async Task<InstructionPatch?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        bool useSession,
        CancellationToken ct)
    {
        var filter = Builders<InstructionPatchDocument>.Filter.And(
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.OwnerUserId, currentUser.UserId),
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        var session = useSession ? sessions.Current : null;
        var doc = session is null
            ? await _collection.Find(filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<InstructionPatch?> GetAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var filter = Builders<InstructionPatchDocument>.Filter.And(
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.Id, id),
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.OwnerUserId, currentUser.UserId));
        var session = sessions.Current;
        var doc = session is null
            ? await _collection.Find(filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<InstructionPatchPage> ListAsync(
        InstructionPatchListFilter filter,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (limit < 1)
        {
            limit = 1;
        }

        var clauses = new List<FilterDefinition<InstructionPatchDocument>>
        {
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.OwnerUserId, currentUser.UserId),
        };
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            clauses.Add(Builders<InstructionPatchDocument>.Filter.Eq(x => x.Status, filter.Status));
        }
        if (!string.IsNullOrWhiteSpace(filter.TargetKind))
        {
            clauses.Add(Builders<InstructionPatchDocument>.Filter.Eq(x => x.TargetKind, filter.TargetKind));
        }
        if (cursor is not null && InstructionPatchCursor.TryDecode(cursor, out var decoded))
        {
            clauses.Add(Builders<InstructionPatchDocument>.Filter.Or(
                Builders<InstructionPatchDocument>.Filter.Lt(x => x.CreatedAt, decoded.CreatedAt),
                Builders<InstructionPatchDocument>.Filter.And(
                    Builders<InstructionPatchDocument>.Filter.Eq(x => x.CreatedAt, decoded.CreatedAt),
                    Builders<InstructionPatchDocument>.Filter.Lt(x => x.Id, decoded.Id))));
        }

        var docs = await _collection
            .Find(Builders<InstructionPatchDocument>.Filter.And(clauses))
            .Sort(Builders<InstructionPatchDocument>.Sort
                .Descending(x => x.CreatedAt)
                .Descending(x => x.Id))
            .Limit(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (docs.Count > limit)
        {
            var pivot = docs[limit - 1];
            nextCursor = InstructionPatchCursor.Encode(pivot.Id, pivot.CreatedAt);
            docs.RemoveRange(limit, docs.Count - limit);
        }

        var items = docs.Select(ToDomain).ToList();
        return new InstructionPatchPage(items, nextCursor);
    }

    public async Task<ApplyInstructionPatchPersistenceOutcome> ApplyAsync(
        InstructionPatch patch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var session = RequireSession(nameof(ApplyAsync));

        var filter = Builders<InstructionPatchDocument>.Filter.And(
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.Id, patch.Id),
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.OwnerUserId, patch.OwnerUserId),
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.Status, InstructionPatchStatusNames.Proposed));

        var update = Builders<InstructionPatchDocument>.Update
            .Set(x => x.Status, patch.Status)
            .Set(x => x.AppliedText, patch.AppliedText)
            .Set(x => x.AppliedInstructionVersion, patch.AppliedInstructionVersion)
            .Set(x => x.UpdatedAt, patch.UpdatedAt.UtcDateTime)
            .Set(x => x.DecidedAt, patch.DecidedAt?.UtcDateTime);

        var result = await _collection.UpdateOneAsync(session, filter, update, options: null, ct);
        if (result.ModifiedCount == 0)
        {
            var fresh = await _collection.Find(session,
                Builders<InstructionPatchDocument>.Filter.And(
                    Builders<InstructionPatchDocument>.Filter.Eq(x => x.Id, patch.Id),
                    Builders<InstructionPatchDocument>.Filter.Eq(x => x.OwnerUserId, patch.OwnerUserId)))
                .FirstOrDefaultAsync(ct);
            return fresh is null
                ? new ApplyInstructionPatchPersistenceOutcome.NotFound()
                : new ApplyInstructionPatchPersistenceOutcome.AlreadyDecided(ToDomain(fresh));
        }

        return new ApplyInstructionPatchPersistenceOutcome.Applied(patch);
    }

    public async Task<RejectInstructionPatchPersistenceOutcome> RejectAsync(
        InstructionPatch patch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var session = RequireSession(nameof(RejectAsync));

        var filter = Builders<InstructionPatchDocument>.Filter.And(
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.Id, patch.Id),
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.OwnerUserId, patch.OwnerUserId),
            Builders<InstructionPatchDocument>.Filter.Eq(x => x.Status, InstructionPatchStatusNames.Proposed));

        var update = Builders<InstructionPatchDocument>.Update
            .Set(x => x.Status, patch.Status)
            .Set(x => x.RejectComment, patch.RejectComment)
            .Set(x => x.UpdatedAt, patch.UpdatedAt.UtcDateTime)
            .Set(x => x.DecidedAt, patch.DecidedAt?.UtcDateTime);

        var result = await _collection.UpdateOneAsync(session, filter, update, options: null, ct);
        if (result.ModifiedCount == 0)
        {
            var fresh = await _collection.Find(session,
                Builders<InstructionPatchDocument>.Filter.And(
                    Builders<InstructionPatchDocument>.Filter.Eq(x => x.Id, patch.Id),
                    Builders<InstructionPatchDocument>.Filter.Eq(x => x.OwnerUserId, patch.OwnerUserId)))
                .FirstOrDefaultAsync(ct);
            return fresh is null
                ? new RejectInstructionPatchPersistenceOutcome.NotFound()
                : new RejectInstructionPatchPersistenceOutcome.AlreadyDecided(ToDomain(fresh));
        }

        return new RejectInstructionPatchPersistenceOutcome.Rejected(patch);
    }

    private IClientSessionHandle RequireSession(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoInstructionPatchRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static InstructionPatchDocument ToDocument(InstructionPatch patch) => new()
    {
        Id = patch.Id,
        OwnerUserId = patch.OwnerUserId,
        TargetKind = patch.TargetKind,
        Status = patch.Status,
        PatchText = patch.PatchText,
        AppliedText = patch.AppliedText,
        EvidenceCardIds = patch.EvidenceCardIds.ToList(),
        Rationale = patch.Rationale,
        RejectComment = patch.RejectComment,
        BaseInstructionVersion = patch.BaseInstructionVersion,
        AppliedInstructionVersion = patch.AppliedInstructionVersion,
        CreatedAt = patch.CreatedAt.UtcDateTime,
        UpdatedAt = patch.UpdatedAt.UtcDateTime,
        DecidedAt = patch.DecidedAt?.UtcDateTime,
    };

    private static InstructionPatch ToDomain(InstructionPatchDocument doc) => InstructionPatch.Restore(
        identity: new InstructionPatchIdentity(
            Id: doc.Id,
            OwnerUserId: string.IsNullOrWhiteSpace(doc.OwnerUserId) ? CurrentUserIds.LocalDev : doc.OwnerUserId,
            TargetKind: doc.TargetKind,
            BaseInstructionVersion: doc.BaseInstructionVersion,
            CreatedAt: ToUtcOffset(doc.CreatedAt)),
        state: new InstructionPatchState(
            Status: doc.Status,
            AppliedText: doc.AppliedText,
            RejectComment: doc.RejectComment,
            AppliedInstructionVersion: doc.AppliedInstructionVersion,
            UpdatedAt: ToUtcOffset(doc.UpdatedAt),
            DecidedAt: doc.DecidedAt is { } d ? ToUtcOffset(d) : null),
        patchText: doc.PatchText,
        evidenceCardIds: doc.EvidenceCardIds ?? new List<string>(),
        rationale: doc.Rationale ?? string.Empty);

    private static DateTimeOffset ToUtcOffset(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
}

/// <summary>
/// Opaque list cursor: base64 of (id, created_at). Mirrors
/// <c>InsightCardCursor</c> so pagination shape is consistent across read
/// surfaces.
/// </summary>
internal static class InstructionPatchCursor
{
    public static string Encode(string id, DateTime createdAtUtc)
    {
        var payload = $"{createdAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out InstructionPatchCursorValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var payload = Encoding.UTF8.GetString(bytes);
            var parts = payload.Split('|', 2);
            if (parts.Length != 2)
            {
                return false;
            }
            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return false;
            }
            value = new InstructionPatchCursorValue(parts[1], new DateTime(ticks, DateTimeKind.Utc));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal readonly record struct InstructionPatchCursorValue(string Id, DateTime CreatedAt);
