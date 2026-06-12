using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.PromptParts;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoPromptPartRepository(IMongoDatabase database, MongoSessionAccessor sessions) : IPromptPartRepository
{
    private const int DuplicateKeyCode = 11000;

    private readonly IMongoCollection<PromptPartDocument> _parts =
        database.GetCollection<PromptPartDocument>(MongoCollectionNames.PromptParts);

    public async Task<CreatePromptPartOutcome> CreateAsync(PromptPart part, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(part);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoPromptPartRepository.CreateAsync must run inside IUnitOfWork.ExecuteAsync.");

        try
        {
            await _parts.InsertOneAsync(session, ToDocument(part), options: null, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == DuplicateKeyCode)
        {
            return new CreatePromptPartOutcome.KeyConflict();
        }

        return new CreatePromptPartOutcome.Created(part);
    }

    public async Task<IReadOnlyList<PromptPart>> ListAsync(CancellationToken ct)
    {
        var filter = Builders<PromptPartDocument>.Filter.Eq(x => x.Scope, InstructionScopeNames.User);
        var session = sessions.Current;
        var documents = session is null
            ? await _parts.Find(filter).SortBy(x => x.Key).ToListAsync(ct)
            : await _parts.Find(session, filter).SortBy(x => x.Key).ToListAsync(ct);

        var result = new List<PromptPart>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(ToDomain(doc));
        }
        return result;
    }

    public async Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var doc = session is null
            ? await _parts.Find(d => d.Id == id.Value).FirstOrDefaultAsync(ct)
            : await _parts.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<ReplacePromptPartTextOutcome> ReplaceTextAsync(
        PromptPartId id,
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
                "MongoPromptPartRepository.ReplaceTextAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _parts.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new ReplacePromptPartTextOutcome.NotFound();
        }
        if (document.CurrentVersion != expectedVersion)
        {
            return new ReplacePromptPartTextOutcome.VersionConflict(document.CurrentVersion);
        }

        var part = ToDomain(document);
        var domainResult = part.ReplaceText(oldText, newText, now);

        switch (domainResult)
        {
            case ReplacePromptPartTextResult.MatchNotFound matchNotFound:
                return new ReplacePromptPartTextOutcome.MatchNotFound(matchNotFound.QueryPreview);

            case ReplacePromptPartTextResult.MatchAmbiguous matchAmbiguous:
                return new ReplacePromptPartTextOutcome.MatchAmbiguous(matchAmbiguous.MatchesCount, matchAmbiguous.MatchLines);

            case ReplacePromptPartTextResult.Replaced:
                {
                    var update = Builders<PromptPartDocument>.Update
                        .Set(d => d.Text, part.Text)
                        .Set(d => d.CurrentVersion, part.CurrentVersion)
                        .Set(d => d.UpdatedAt, part.UpdatedAt.UtcDateTime);

                    var updateResult = await _parts.UpdateOneAsync(
                        session,
                        d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
                        update,
                        options: null,
                        ct);

                    if (updateResult.ModifiedCount == 0)
                    {
                        var fresh = await _parts.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
                        return new ReplacePromptPartTextOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
                    }

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

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoPromptPartRepository.SetModeRolesAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _parts.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return null;
        }

        var part = ToDomain(document);
        part.SetModeRoles(modeRoles, now);

        var update = Builders<PromptPartDocument>.Update
            .Set(d => d.ModeRoles, MapRoles(part.ModeRoles))
            .Set(d => d.UpdatedAt, part.UpdatedAt.UtcDateTime);

        await _parts.UpdateOneAsync(session, d => d.Id == id.Value, update, options: null, ct);
        return part;
    }

    public async Task<DeletePromptPartOutcome> DeleteAsync(PromptPartId id, CancellationToken ct)
    {
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoPromptPartRepository.DeleteAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _parts.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct);
        if (document is null)
        {
            return new DeletePromptPartOutcome.NotFound();
        }
        if (document.ModeRoles.Count > 0)
        {
            return new DeletePromptPartOutcome.HasRoles();
        }

        await _parts.DeleteOneAsync(session, d => d.Id == id.Value, options: null, ct);
        return new DeletePromptPartOutcome.Deleted();
    }

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
