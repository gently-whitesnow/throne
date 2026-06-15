using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoTextVersionRepository
    : MongoRepositoryBase<TextVersionDocument, string>, ITextVersionRepository
{
    public MongoTextVersionRepository(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.TextVersions, sessions)
    {
    }

    protected override FilterDefinition<TextVersionDocument> ById(string id) =>
        Builders<TextVersionDocument>.Filter.Eq(d => d.Id, id);

    public async Task<IReadOnlyList<TextVersion>> ListByOwnerAsync(
        TextVersionOwnerKind ownerKind,
        string ownerId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var ownerKindWire = ownerKind.ToWire();
        var filter = Builders<TextVersionDocument>.Filter.And(
            Builders<TextVersionDocument>.Filter.Eq(d => d.OwnerKind, ownerKindWire),
            Builders<TextVersionDocument>.Filter.Eq(d => d.OwnerId, ownerId));

        var documents = await Find(filter).SortBy(d => d.Version).ToListAsync(ct);

        var result = new List<TextVersion>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(MapToDomain(doc));
        }

        return result;
    }

    private static TextVersion MapToDomain(TextVersionDocument doc) => new(
        Id: doc.Id,
        OwnerKind: ParseOwnerKind(doc.OwnerKind),
        OwnerId: doc.OwnerId,
        Version: doc.Version,
        Kind: ParseKind(doc.Kind),
        Delta: new TextVersionDelta(doc.Snapshot, doc.OldText, doc.NewText, doc.AfterLine, doc.InsertText),
        ChangedAt: DateTime.SpecifyKind(doc.ChangedAt, DateTimeKind.Utc),
        ChangedBy: ParseAuthor(doc.ChangedBy));

    private static TextVersionOwnerKind ParseOwnerKind(string wire) => wire switch
    {
        "intent" => TextVersionOwnerKind.Intent,
        "instruction" => TextVersionOwnerKind.Instruction,
        "prompt_part" => TextVersionOwnerKind.PromptPart,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown owner_kind."),
    };

    private static TextVersionKind ParseKind(string wire) => wire switch
    {
        "create" => TextVersionKind.Create,
        "replace" => TextVersionKind.Replace,
        "insert" => TextVersionKind.Insert,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown kind."),
    };

    private static TextVersionAuthor ParseAuthor(string wire) => wire switch
    {
        "user" => TextVersionAuthor.User,
        "agent" => TextVersionAuthor.Agent,
        "system" => TextVersionAuthor.System,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown changed_by."),
    };
}
