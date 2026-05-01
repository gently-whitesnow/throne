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

    public async Task<IReadOnlyList<Instruction>> GetByKindsAsync(IReadOnlyList<string> kinds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        if (kinds.Count == 0)
        {
            return [];
        }

        var filter = Builders<InstructionDocument>.Filter.In(x => x.Kind, kinds);
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
        var filter = Builders<InstructionDocument>.Filter.Empty;
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

    private static InstructionDocument MapInstruction(Instruction instruction) => new()
    {
        Id = instruction.Id.Value,
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
        kind: doc.Kind,
        text: doc.Text,
        currentVersion: doc.CurrentVersion,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));
}
