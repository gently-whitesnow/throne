using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoPromptPartRepository
    : MongoRepositoryBase<PromptPartDocument, string>, IPromptPartRepository
{
    // Versions live in a sibling collection — only the primary `prompt_parts` collection
    // is wired through the base, the history feed stays on its own field.
    private readonly IMongoCollection<TextVersionDocument> _textVersions;

    public MongoPromptPartRepository(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.PromptParts, sessions)
    {
        _textVersions = database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);
    }

    protected override FilterDefinition<PromptPartDocument> ById(string id) =>
        Builders<PromptPartDocument>.Filter.Eq(d => d.Id, id);

    public async Task<CreatePromptPartOutcome> CreateAsync(PromptPart part, TextVersion initialVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(initialVersion);

        var session = RequireSession(nameof(CreateAsync));

        var existing = await Collection.Find(
                session,
                d => d.Scope == part.Scope && d.Key == part.Key)
            .Project(d => d.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return new CreatePromptPartOutcome.KeyConflict();
        }

        await InsertOneAsync(ToDocument(part), ct);
        await _textVersions.InsertOneAsync(session, MapVersion(initialVersion), options: null, ct);

        return new CreatePromptPartOutcome.Created(part);
    }

    public async Task<IReadOnlyList<PromptPart>> ListAsync(string? scope, CancellationToken ct)
    {
        var filter = scope is null
            ? Builders<PromptPartDocument>.Filter.Empty
            : Builders<PromptPartDocument>.Filter.Eq(x => x.Scope, scope);
        var documents = await Find(filter).SortBy(x => x.Scope).ThenBy(x => x.Key).ToListAsync(ct);

        var result = new List<PromptPart>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(ToDomain(doc));
        }
        return result;
    }

    public async Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct)
    {
        var doc = await FindByIdAsync(id.Value, ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<PromptPart?> GetByScopeKeyAsync(string scope, string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var filter = Builders<PromptPartDocument>.Filter.And(
            Builders<PromptPartDocument>.Filter.Eq(x => x.Scope, scope),
            Builders<PromptPartDocument>.Filter.Eq(x => x.Key, key));
        var doc = await FindOneAsync(filter, ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<IReadOnlyList<PromptPart>> GetByScopeAndKeysAsync(
        string scope,
        IReadOnlyList<string> keys,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return [];
        }

        var filter = Builders<PromptPartDocument>.Filter.And(
            Builders<PromptPartDocument>.Filter.Eq(x => x.Scope, scope),
            Builders<PromptPartDocument>.Filter.In(x => x.Key, keys));
        var documents = await Find(filter).SortBy(x => x.Key).ToListAsync(ct);

        var result = new List<PromptPart>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(ToDomain(doc));
        }
        return result;
    }

    public async Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
        PromptPartId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var session = RequireSession(nameof(ReplaceTextAsync));

        var document = await FindByIdAsync(id.Value, ct);
        if (document is null)
        {
            return new ReplacePromptPartTextOutcome.NotFound();
        }
        if (document.CurrentVersion != expectedVersion)
        {
            return new ReplacePromptPartTextOutcome.VersionConflict(document.CurrentVersion);
        }

        var part = ToDomain(document);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = part.ReplaceText(oldText, newText, newVersionId, now, changedBy);

        switch (domainResult)
        {
            case ReplacePromptPartTextResult.MatchNotFound matchNotFound:
                return new ReplacePromptPartTextOutcome.MatchNotFound(matchNotFound.QueryPreview);

            case ReplacePromptPartTextResult.MatchAmbiguous matchAmbiguous:
                return new ReplacePromptPartTextOutcome.MatchAmbiguous(matchAmbiguous.MatchesCount, matchAmbiguous.MatchLines);

            case ReplacePromptPartTextResult.Replaced replaced:
                {
                    var update = Builders<PromptPartDocument>.Update
                        .Set(d => d.Text, part.Text)
                        .Set(d => d.CurrentVersion, part.CurrentVersion)
                        .Set(d => d.UpdatedAt, part.UpdatedAt.UtcDateTime);
                    var casFilter = Builders<PromptPartDocument>.Filter.And(
                        ById(id.Value),
                        Builders<PromptPartDocument>.Filter.Eq(d => d.CurrentVersion, expectedVersion));

                    if (!await TryUpdateAsync(casFilter, update, ct))
                    {
                        var fresh = await FindByIdAsync(id.Value, ct);
                        return new ReplacePromptPartTextOutcome.VersionConflict(
                            fresh?.CurrentVersion ?? expectedVersion);
                    }

                    await _textVersions.InsertOneAsync(session, MapVersion(replaced.Version), options: null, ct);
                    return new ReplacePromptPartTextOutcome.Replaced(part);
                }

            default:
                throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}");
        }
    }

    public async Task<PromptPart?> SetModeRolesAsync(
        PromptPartId id,
        IReadOnlyList<PromptPartModeRole> modeRoles,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(modeRoles);
        _ = RequireSession(nameof(SetModeRolesAsync));

        var document = await FindByIdAsync(id.Value, ct);
        if (document is null)
        {
            return null;
        }

        var part = ToDomain(document);
        part.SetModeRoles(modeRoles, now);

        var update = Builders<PromptPartDocument>.Update
            .Set(d => d.ModeRoles, MapRoles(part.ModeRoles))
            .Set(d => d.UpdatedAt, part.UpdatedAt.UtcDateTime);

        await TryUpdateAsync(ById(id.Value), update, ct);
        return part;
    }

    public async Task<DeletePromptPartOutcome> DeleteAsync(PromptPartId id, CancellationToken ct)
    {
        _ = RequireSession(nameof(DeleteAsync));

        var document = await FindByIdAsync(id.Value, ct);
        if (document is null)
        {
            return new DeletePromptPartOutcome.NotFound();
        }
        if (document.ModeRoles.Count > 0)
        {
            return new DeletePromptPartOutcome.HasRoles();
        }

        await DeleteOneAsync(ById(id.Value), ct);
        return new DeletePromptPartOutcome.Deleted();
    }

    private IClientSessionHandle RequireSession(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoPromptPartRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static PromptPartDocument ToDocument(PromptPart part) => new()
    {
        Id = part.Id.Value,
        Scope = part.Scope,
        Key = part.Key,
        Text = part.Text,
        Description = part.Description,
        CurrentVersion = part.CurrentVersion,
        ModeRoles = MapRoles(part.ModeRoles),
        CreatedAt = part.CreatedAt.UtcDateTime,
        UpdatedAt = part.UpdatedAt.UtcDateTime,
    };

    private static List<PromptPartModeRoleDocument> MapRoles(IReadOnlyList<PromptPartModeRole> roles) =>
        roles.Select(r => new PromptPartModeRoleDocument { Mode = r.Mode, Role = r.Role, Order = r.Order }).ToList();

    private static TextVersionDocument MapVersion(TextVersion v) => new()
    {
        Id = v.Id,
        OwnerKind = v.OwnerKind.ToWire(),
        OwnerId = v.OwnerId,
        Version = v.Version,
        Kind = v.Kind.ToWire(),
        Snapshot = v.Delta.Snapshot,
        OldText = v.Delta.OldText,
        NewText = v.Delta.NewText,
        AfterLine = v.Delta.AfterLine,
        InsertText = v.Delta.InsertText,
        ChangedAt = v.ChangedAt.UtcDateTime,
        ChangedBy = v.ChangedBy.ToWire(),
    };

    private static PromptPart ToDomain(PromptPartDocument doc) => PromptPart.Restore(
        id: new PromptPartId(doc.Id),
        scope: doc.Scope,
        key: doc.Key,
        text: doc.Text,
        description: doc.Description,
        currentVersion: doc.CurrentVersion,
        modeRoles: doc.ModeRoles.Select(r => new PromptPartModeRole(r.Mode, r.Role, r.Order)).ToList(),
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));
}
