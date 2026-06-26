using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Linking;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Rows;
using DomainLinkPayload = Throne.Domain.Intents.Events.IntentEventLinkPayload;
using DomainTextChange = Throne.Domain.Intents.Events.IntentEventTextChange;
using RowLinkPayload = Throne.Infrastructure.EfCore.Rows.IntentEventLinkPayload;
using RowTextChange = Throne.Infrastructure.EfCore.Rows.IntentEventTextChange;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class IntentEventRowMapper
{
    public static IntentEventRow ToRow(IntentEvent e) => new()
    {
        Id = e.Id,
        IntentId = e.IntentId.Value,
        PeerIntentId = e.PeerIntentId?.Value,
        Kind = e.Kind.ToWire(),
        Version = e.Version,
        TextChange = e.TextChange is null ? null : new RowTextChange
        {
            Kind = e.TextChange.Kind.ToWire(),
            Snapshot = e.TextChange.Snapshot,
            OldText = e.TextChange.OldText,
            NewText = e.TextChange.NewText,
            AfterLine = e.TextChange.AfterLine,
            InsertText = e.TextChange.InsertText,
        },
        Link = e.Link is null ? null : new RowLinkPayload
        {
            Id = e.Link.Id,
            FromId = e.Link.FromId,
            ToId = e.Link.ToId,
            Blocking = e.Link.Blocking,
            Author = e.Link.Author.ToWire(),
            Rationale = e.Link.Rationale,
            CreatedAt = e.Link.CreatedAt,
        },
        CreatedAt = e.Audit.CreatedAt,
        CreatedBy = e.Audit.CreatedBy?.ToWire(),
    };

    public static IntentEvent ToDomain(IntentEventRow row) => new(
        Id: row.Id,
        IntentId: new IntentId(row.IntentId),
        PeerIntentId: string.IsNullOrEmpty(row.PeerIntentId) ? null : new IntentId(row.PeerIntentId),
        Kind: IntentEventKindExtensions.FromWire(row.Kind),
        Version: row.Version,
        TextChange: row.TextChange is null ? null : new DomainTextChange(
            Kind: EfEnumWires.ParseTextVersionKind(row.TextChange.Kind),
            Snapshot: row.TextChange.Snapshot,
            OldText: row.TextChange.OldText,
            NewText: row.TextChange.NewText,
            AfterLine: row.TextChange.AfterLine,
            InsertText: row.TextChange.InsertText),
        Link: row.Link is null ? null : new DomainLinkPayload(
            Id: row.Link.Id,
            FromId: row.Link.FromId,
            ToId: row.Link.ToId,
            Blocking: row.Link.Blocking,
            Author: IntentLinkAuthorExtensions.FromWire(row.Link.Author),
            Rationale: row.Link.Rationale,
            CreatedAt: row.Link.CreatedAt),
        Audit: new IntentEventAudit(
            CreatedAt: row.CreatedAt,
            CreatedBy: string.IsNullOrEmpty(row.CreatedBy) ? null : IntentEventKindExtensions.AuthorFromWire(row.CreatedBy)));
}
