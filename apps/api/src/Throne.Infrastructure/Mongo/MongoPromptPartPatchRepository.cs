using System.Globalization;
using System.Text;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Mongo persistence for <see cref="PromptPartPatch"/>.
/// </summary>
internal sealed class MongoPromptPartPatchRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions) : IPromptPartPatchRepository
{
    private readonly IMongoCollection<PromptPartPatchDocument> _collection =
        database.GetCollection<PromptPartPatchDocument>(MongoCollectionNames.PromptPartPatches);

    public async Task<CreatePromptPartPatchOutcome> CreateAsync(
        PromptPartPatch patch,
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
            return new CreatePromptPartPatchOutcome(patch);
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
            return new CreatePromptPartPatchOutcome(existing, IsExisting: true);
        }
    }

    public Task<PromptPartPatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return FindByIdempotencyKeyAsync(idempotencyKey, useSession: true, ct);
    }

    private async Task<PromptPartPatch?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        bool useSession,
        CancellationToken ct)
    {
        var filter = Builders<PromptPartPatchDocument>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey);
        var session = useSession ? sessions.Current : null;
        var doc = session is null
            ? await _collection.Find(filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<PromptPartPatch?> GetAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var filter = Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Id, id);
        var session = sessions.Current;
        var doc = session is null
            ? await _collection.Find(filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(session, filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<PromptPartPatchPage> ListAsync(
        PromptPartPatchListFilter filter,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (limit < 1)
        {
            limit = 1;
        }

        var clauses = new List<FilterDefinition<PromptPartPatchDocument>>();
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            clauses.Add(Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Status, filter.Status));
        }
        if (!string.IsNullOrWhiteSpace(filter.TargetScope))
        {
            clauses.Add(Builders<PromptPartPatchDocument>.Filter.Eq(x => x.TargetScope, filter.TargetScope));
        }
        if (!string.IsNullOrWhiteSpace(filter.TargetKey))
        {
            clauses.Add(Builders<PromptPartPatchDocument>.Filter.Eq(x => x.TargetKey, filter.TargetKey));
        }
        if (cursor is not null && PromptPartPatchCursor.TryDecode(cursor, out var decoded))
        {
            clauses.Add(Builders<PromptPartPatchDocument>.Filter.Or(
                Builders<PromptPartPatchDocument>.Filter.Lt(x => x.CreatedAt, decoded.CreatedAt),
                Builders<PromptPartPatchDocument>.Filter.And(
                    Builders<PromptPartPatchDocument>.Filter.Eq(x => x.CreatedAt, decoded.CreatedAt),
                    Builders<PromptPartPatchDocument>.Filter.Lt(x => x.Id, decoded.Id))));
        }

        var combined = clauses.Count == 0
            ? Builders<PromptPartPatchDocument>.Filter.Empty
            : Builders<PromptPartPatchDocument>.Filter.And(clauses);
        var docs = await _collection
            .Find(combined)
            .Sort(Builders<PromptPartPatchDocument>.Sort
                .Descending(x => x.CreatedAt)
                .Descending(x => x.Id))
            .Limit(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (docs.Count > limit)
        {
            var pivot = docs[limit - 1];
            nextCursor = PromptPartPatchCursor.Encode(pivot.Id, pivot.CreatedAt);
            docs.RemoveRange(limit, docs.Count - limit);
        }

        var items = docs.Select(ToDomain).ToList();
        return new PromptPartPatchPage(items, nextCursor);
    }

    public async Task<ApplyPromptPartPatchPersistenceOutcome> ApplyAsync(
        PromptPartPatch patch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var session = RequireSession(nameof(ApplyAsync));

        var filter = Builders<PromptPartPatchDocument>.Filter.And(
            Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Id, patch.Identity.Id),
            Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Status, PromptPartPatchStatusNames.Proposed));

        var update = Builders<PromptPartPatchDocument>.Update
            .Set(x => x.Status, patch.State.Status)
            .Set(x => x.AppliedText, patch.State.AppliedText)
            .Set(x => x.AppliedVersion, patch.State.AppliedVersion)
            .Set(x => x.UpdatedAt, patch.State.UpdatedAt.UtcDateTime)
            .Set(x => x.DecidedAt, patch.State.DecidedAt?.UtcDateTime);

        var result = await _collection.UpdateOneAsync(session, filter, update, options: null, ct);
        if (result.ModifiedCount == 0)
        {
            var fresh = await _collection.Find(session,
                Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Id, patch.Identity.Id))
                .FirstOrDefaultAsync(ct);
            return fresh is null
                ? new ApplyPromptPartPatchPersistenceOutcome.NotFound()
                : new ApplyPromptPartPatchPersistenceOutcome.AlreadyDecided(ToDomain(fresh));
        }

        return new ApplyPromptPartPatchPersistenceOutcome.Applied(patch);
    }

    public async Task<RejectPromptPartPatchPersistenceOutcome> RejectAsync(
        PromptPartPatch patch,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var session = RequireSession(nameof(RejectAsync));

        var filter = Builders<PromptPartPatchDocument>.Filter.And(
            Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Id, patch.Identity.Id),
            Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Status, PromptPartPatchStatusNames.Proposed));

        var update = Builders<PromptPartPatchDocument>.Update
            .Set(x => x.Status, patch.State.Status)
            .Set(x => x.RejectComment, patch.State.RejectComment)
            .Set(x => x.UpdatedAt, patch.State.UpdatedAt.UtcDateTime)
            .Set(x => x.DecidedAt, patch.State.DecidedAt?.UtcDateTime);

        var result = await _collection.UpdateOneAsync(session, filter, update, options: null, ct);
        if (result.ModifiedCount == 0)
        {
            var fresh = await _collection.Find(session,
                Builders<PromptPartPatchDocument>.Filter.Eq(x => x.Id, patch.Identity.Id))
                .FirstOrDefaultAsync(ct);
            return fresh is null
                ? new RejectPromptPartPatchPersistenceOutcome.NotFound()
                : new RejectPromptPartPatchPersistenceOutcome.AlreadyDecided(ToDomain(fresh));
        }

        return new RejectPromptPartPatchPersistenceOutcome.Rejected(patch);
    }

    private IClientSessionHandle RequireSession(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoPromptPartPatchRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static PromptPartPatchDocument ToDocument(PromptPartPatch patch) => new()
    {
        Id = patch.Identity.Id,
        TargetScope = patch.Identity.TargetScope,
        TargetKey = patch.Identity.TargetKey,
        Status = patch.State.Status,
        Operation = patch.Operation,
        PatchText = patch.PatchText,
        ModeRoles = patch.ModeRoles?.Select(r => new PromptPartModeRoleDocument
        {
            Mode = r.Mode,
            Role = r.Role,
            Order = r.Order,
        }).ToList(),
        AppliedText = patch.State.AppliedText,
        EvidenceCardIds = patch.EvidenceCardIds.ToList(),
        Rationale = patch.Rationale,
        RejectComment = patch.State.RejectComment,
        BaseVersion = patch.Identity.BaseVersion,
        AppliedVersion = patch.State.AppliedVersion,
        CreatedAt = patch.Identity.CreatedAt.UtcDateTime,
        UpdatedAt = patch.State.UpdatedAt.UtcDateTime,
        DecidedAt = patch.State.DecidedAt?.UtcDateTime,
    };

    private static PromptPartPatch ToDomain(PromptPartPatchDocument doc) => PromptPartPatch.Restore(
        identity: new PromptPartPatchIdentity(
            Id: doc.Id,
            TargetScope: doc.TargetScope,
            TargetKey: doc.TargetKey,
            BaseVersion: doc.BaseVersion,
            CreatedAt: ToUtcOffset(doc.CreatedAt)),
        state: new PromptPartPatchState(
            Status: doc.Status,
            AppliedText: doc.AppliedText,
            RejectComment: doc.RejectComment,
            AppliedVersion: doc.AppliedVersion,
            UpdatedAt: ToUtcOffset(doc.UpdatedAt),
            DecidedAt: doc.DecidedAt is { } d ? ToUtcOffset(d) : null),
        operation: doc.Operation,
        patchText: doc.PatchText,
        modeRoles: doc.ModeRoles?.Select(r => new PromptPartModeRole(r.Mode, r.Role, r.Order)).ToList(),
        evidenceCardIds: doc.EvidenceCardIds ?? new List<string>(),
        rationale: doc.Rationale ?? string.Empty);

    private static DateTimeOffset ToUtcOffset(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
}

/// <summary>
/// Opaque list cursor: base64 of (id, created_at).
/// </summary>
internal static class PromptPartPatchCursor
{
    public static string Encode(string id, DateTime createdAtUtc)
    {
        var payload = $"{createdAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out PromptPartPatchCursorValue value)
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
            value = new PromptPartPatchCursorValue(parts[1], new DateTime(ticks, DateTimeKind.Utc));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal readonly record struct PromptPartPatchCursorValue(string Id, DateTime CreatedAt);
