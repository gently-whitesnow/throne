using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Linking;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentEventRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions) : IIntentEventRepository
{
    private readonly IMongoCollection<IntentEventDocument> _events =
        database.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents);

    public async Task<IReadOnlyList<IntentEvent>> ListByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var fb = Builders<IntentEventDocument>.Filter;
        var filter = fb.Or(
            fb.Eq(d => d.IntentId, intentId.Value),
            fb.Eq(d => d.PeerIntentId, intentId.Value));

        var session = sessions.Current;
        var find = session is null ? _events.Find(filter) : _events.Find(session, filter);
        var docs = await find.SortBy(d => d.CreatedAt).ToListAsync(ct);

        return docs.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<IntentEvent>> ListTextChangesAsync(IntentId intentId, CancellationToken ct)
    {
        var fb = Builders<IntentEventDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.IntentId, intentId.Value),
            fb.Eq(d => d.Kind, IntentEventKind.TextChanged.ToWire()));

        var session = sessions.Current;
        var find = session is null ? _events.Find(filter) : _events.Find(session, filter);
        var docs = await find.SortBy(d => d.Version).ToListAsync(ct);

        return docs.Select(MapToDomain).ToList();
    }

    public async Task AppendAsync(IntentEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var session = sessions.Current
            ?? throw new InvalidOperationException(
                "MongoIntentEventRepository.AppendAsync must run inside IUnitOfWork.ExecuteAsync.");
        await _events.InsertOneAsync(session, MapToDocument(evt), options: null, ct);
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
            Type = e.Link.Type,
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
            Type: d.Link.Type,
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
