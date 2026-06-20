using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Linking;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentEventRepository
    : MongoRepositoryBase<IntentEventDocument, string>, IIntentEventRepository
{
    public MongoIntentEventRepository(IMongoDatabase database, MongoSessionAccessor sessions)
        : base(database, MongoCollectionNames.IntentEvents, sessions)
    {
    }

    protected override FilterDefinition<IntentEventDocument> ById(string id) =>
        Builders<IntentEventDocument>.Filter.Eq(d => d.Id, id);

    public async Task<IReadOnlyList<IntentEvent>> ListByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var fb = Builders<IntentEventDocument>.Filter;
        var filter = fb.Or(
            fb.Eq(d => d.IntentId, intentId.Value),
            fb.Eq(d => d.PeerIntentId, intentId.Value));

        var docs = await Find(filter).SortBy(d => d.CreatedAt).ToListAsync(ct);
        return docs.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<IntentEvent>> ListTextChangesAsync(IntentId intentId, CancellationToken ct)
    {
        var fb = Builders<IntentEventDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.IntentId, intentId.Value),
            fb.Eq(d => d.Kind, IntentEventKind.TextChanged.ToWire()));

        var docs = await Find(filter).SortBy(d => d.Version).ToListAsync(ct);
        return docs.Select(MapToDomain).ToList();
    }

    public async Task AppendAsync(IntentEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        // Writes need an ambient session — the base helper would silently drop to
        // sessionless, which would break event ↔ aggregate atomicity.
        if (Sessions.Current is null)
        {
            throw new InvalidOperationException(
                "MongoIntentEventRepository.AppendAsync must run inside IUnitOfWork.ExecuteAsync.");
        }
        await InsertOneAsync(MapToDocument(evt), ct);
    }

    private static IntentEventDocument MapToDocument(IntentEvent e) => new()
    {
        Id = e.Id,
        IntentId = e.IntentId.Value,
        PeerIntentId = e.PeerIntentId?.Value,
        Kind = e.Kind.ToWire(),
        Version = e.Version,
        TextChange = e.TextChange is null ? null : new IntentEventTextChangeSubdocument
        {
            Kind = e.TextChange.Kind switch
            {
                Throne.Domain.TextVersions.TextVersionKind.Create => "create",
                Throne.Domain.TextVersions.TextVersionKind.Replace => "replace",
                Throne.Domain.TextVersions.TextVersionKind.Insert => "insert",
                _ => throw new InvalidOperationException($"Unknown text change kind: {e.TextChange.Kind}."),
            },
            Snapshot = e.TextChange.Snapshot,
            OldText = e.TextChange.OldText,
            NewText = e.TextChange.NewText,
            AfterLine = e.TextChange.AfterLine,
            InsertText = e.TextChange.InsertText,
        },
        Link = e.Link is null ? null : new IntentEventLinkSubdocument
        {
            Id = e.Link.Id,
            FromId = e.Link.FromId,
            ToId = e.Link.ToId,
            Blocking = e.Link.Blocking,
            Author = e.Link.Author.ToWire(),
            Rationale = e.Link.Rationale,
            CreatedAt = e.Link.CreatedAt.UtcDateTime,
        },
        CreatedAt = e.Audit.CreatedAt.UtcDateTime,
        CreatedBy = e.Audit.CreatedBy?.ToWire(),
    };

    private static IntentEvent MapToDomain(IntentEventDocument d) => new(
        Id: d.Id,
        IntentId: new IntentId(d.IntentId),
        PeerIntentId: string.IsNullOrEmpty(d.PeerIntentId) ? null : new IntentId(d.PeerIntentId),
        Kind: IntentEventKindExtensions.FromWire(d.Kind),
        Version: d.Version,
        TextChange: d.TextChange is null ? null : new IntentEventTextChange(
            Kind: ParseTextKind(d.TextChange.Kind),
            Snapshot: d.TextChange.Snapshot,
            OldText: d.TextChange.OldText,
            NewText: d.TextChange.NewText,
            AfterLine: d.TextChange.AfterLine,
            InsertText: d.TextChange.InsertText),
        Link: d.Link is null ? null : new IntentEventLinkPayload(
            Id: d.Link.Id,
            FromId: d.Link.FromId,
            ToId: d.Link.ToId,
            Blocking: d.Link.Blocking,
            Author: IntentLinkAuthorExtensions.FromWire(d.Link.Author),
            Rationale: d.Link.Rationale,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(d.Link.CreatedAt, DateTimeKind.Utc))),
        Audit: new IntentEventAudit(
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc)),
            CreatedBy: string.IsNullOrEmpty(d.CreatedBy) ? null : IntentEventKindExtensions.AuthorFromWire(d.CreatedBy)));

    private static Throne.Domain.TextVersions.TextVersionKind ParseTextKind(string wire) => wire switch
    {
        "create" => Throne.Domain.TextVersions.TextVersionKind.Create,
        "replace" => Throne.Domain.TextVersions.TextVersionKind.Replace,
        "insert" => Throne.Domain.TextVersions.TextVersionKind.Insert,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown text change kind."),
    };
}
