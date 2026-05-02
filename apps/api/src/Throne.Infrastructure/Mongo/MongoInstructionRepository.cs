using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoInstructionRepository(IMongoDatabase database, MongoSessionAccessor sessions) : IInstructionRepository
{
    private readonly IMongoCollection<InstructionDocument> _instructions =
        database.GetCollection<InstructionDocument>(MongoCollectionNames.Instructions);

    private readonly IMongoCollection<TextVersionDocument> _textVersions =
        database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);

    public async Task CreateAsync(Instruction instruction, TextVersion initialVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(initialVersion);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoInstructionRepository.CreateAsync must run inside IUnitOfWork.ExecuteAsync.");

        await _textVersions.InsertOneAsync(session, MapVersion(initialVersion), options: null, ct).ConfigureAwait(false);
        await _instructions.InsertOneAsync(session, MapInstruction(instruction), options: null, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Instruction>> GetUserInstructionsByKindsAsync(
        string userId,
        IReadOnlyList<string> kinds,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(kinds);
        if (kinds.Count == 0)
        {
            return [];
        }

        var filter = Builders<InstructionDocument>.Filter.And(
            Builders<InstructionDocument>.Filter.Eq(x => x.Scope, InstructionScopeNames.User),
            Builders<InstructionDocument>.Filter.Eq(x => x.UserId, userId),
            Builders<InstructionDocument>.Filter.In(x => x.Kind, kinds));

        var session = sessions.Current;
        var documents = session is null
            ? await _instructions.Find(filter).SortBy(x => x.Kind).ThenBy(x => x.CreatedAt)
                .ToListAsync(ct).ConfigureAwait(false)
            : await _instructions.Find(session, filter).SortBy(x => x.Kind).ThenBy(x => x.CreatedAt)
                .ToListAsync(ct).ConfigureAwait(false);

        var result = new List<Instruction>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(MapToDomain(doc));
        }

        return result;
    }

    public async Task<IReadOnlyList<Instruction>> ListAsync(CancellationToken ct)
    {
        var filter = Builders<InstructionDocument>.Filter.Eq(x => x.Scope, InstructionScopeNames.User);
        var session = sessions.Current;
        var documents = session is null
            ? await _instructions.Find(filter).SortBy(x => x.Kind).ThenBy(x => x.CreatedAt)
                .ToListAsync(ct).ConfigureAwait(false)
            : await _instructions.Find(session, filter).SortBy(x => x.Kind).ThenBy(x => x.CreatedAt)
                .ToListAsync(ct).ConfigureAwait(false);

        var result = new List<Instruction>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(MapToDomain(doc));
        }

        return result;
    }

    public async Task<Instruction?> GetByIdAsync(InstructionId id, CancellationToken ct)
    {
        var filter = Builders<InstructionDocument>.Filter.Eq(x => x.Id, id.Value);
        var session = sessions.Current;
        var doc = session is null
            ? await _instructions.Find(filter).FirstOrDefaultAsync(ct).ConfigureAwait(false)
            : await _instructions.Find(session, filter).FirstOrDefaultAsync(ct).ConfigureAwait(false);

        return doc is null ? null : MapToDomain(doc);
    }

    public async Task<ReplaceInstructionTextOutcome> ReplaceTextAsync(
        InstructionId id,
        int expectedVersion,
        string oldText,
        string newText,
        TextVersionAuthor changedBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(oldText);
        ArgumentNullException.ThrowIfNull(newText);

        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoInstructionRepository.ReplaceTextAsync must run inside IUnitOfWork.ExecuteAsync.");

        var document = await _instructions.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (document is null)
        {
            return new ReplaceInstructionTextOutcome.NotFound();
        }

        if (document.CurrentVersion != expectedVersion)
        {
            return new ReplaceInstructionTextOutcome.VersionConflict(document.CurrentVersion);
        }

        var instruction = MapToDomain(document);
        var newVersionId = Guid.NewGuid().ToString("N");
        var domainResult = instruction.ReplaceText(oldText, newText, newVersionId, now, changedBy);

        switch (domainResult)
        {
            case ReplaceInstructionTextResult.MatchNotFound matchNotFound:
                return new ReplaceInstructionTextOutcome.MatchNotFound(matchNotFound.QueryPreview);

            case ReplaceInstructionTextResult.MatchAmbiguous matchAmbiguous:
                return new ReplaceInstructionTextOutcome.MatchAmbiguous(matchAmbiguous.MatchesCount, matchAmbiguous.MatchLines);

            case ReplaceInstructionTextResult.Replaced replaced:
                {
                    var update = Builders<InstructionDocument>.Update
                        .Set(d => d.Text, instruction.Text)
                        .Set(d => d.CurrentVersion, instruction.CurrentVersion)
                        .Set(d => d.UpdatedAt, instruction.UpdatedAt.UtcDateTime);

                    var updateResult = await _instructions.UpdateOneAsync(
                        session,
                        d => d.Id == id.Value && d.CurrentVersion == expectedVersion,
                        update,
                        options: null,
                        ct).ConfigureAwait(false);

                    if (updateResult.ModifiedCount == 0)
                    {
                        var fresh = await _instructions.Find(session, d => d.Id == id.Value).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                        return new ReplaceInstructionTextOutcome.VersionConflict(fresh?.CurrentVersion ?? expectedVersion);
                    }

                    await _textVersions.InsertOneAsync(session, MapVersion(replaced.Version), options: null, ct).ConfigureAwait(false);
                    return new ReplaceInstructionTextOutcome.Replaced(instruction);
                }

            default:
                throw new InvalidOperationException($"Unhandled domain result: {domainResult.GetType().Name}");
        }
    }

    private static InstructionDocument MapInstruction(Instruction instruction) => new()
    {
        Id = instruction.Id.Value,
        Scope = instruction.Scope,
        UserId = instruction.UserId,
        Kind = instruction.Kind,
        Text = instruction.Text,
        CurrentVersion = instruction.CurrentVersion,
        CreatedAt = instruction.CreatedAt.UtcDateTime,
        UpdatedAt = instruction.UpdatedAt.UtcDateTime,
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

    private static Instruction MapToDomain(InstructionDocument doc) => Instruction.Restore(
        id: new InstructionId(doc.Id),
        scope: doc.Scope,
        userId: doc.UserId,
        kind: doc.Kind,
        text: doc.Text,
        currentVersion: doc.CurrentVersion,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));
}
