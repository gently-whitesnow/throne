using Throne.Domain.Intents.Events;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkAuthor = Throne.Intents.Contracts.Generated.IntentLinkAuthor;

namespace Throne.Api.Intents;

internal static class IntentEventDtoMapper
{
    public static IntentEventDto ToEventDto(IntentEvent e) => new()
    {
        Id = e.Id,
        Intent_id = e.IntentId.Value,
        Peer_intent_id = e.PeerIntentId?.Value,
        Kind = ToKind(e.Kind),
        Version = e.Version ?? 0,
        Text_change = e.TextChange is null ? null : ToTextChange(e.TextChange),
        Link = e.Link is null ? null : ToLinkPayload(e.Link),
        Created_at = e.Audit.CreatedAt,
        Created_by = ToCreatedBy(e.Audit.CreatedBy),
    };

    private static IntentEventDtoKind ToKind(IntentEventKind kind) => kind switch
    {
        IntentEventKind.TextChanged => IntentEventDtoKind.Text_changed,
        IntentEventKind.LinkAdded => IntentEventDtoKind.Link_added,
        IntentEventKind.LinkRemoved => IntentEventDtoKind.Link_removed,
        _ => throw new InvalidOperationException($"Unknown intent_event kind: {kind}"),
    };

    private static IntentEventTextChangeDto ToTextChange(IntentEventTextChange change) => new()
    {
        Kind = change.Kind switch
        {
            TextVersionKind.Create => IntentEventTextChangeDtoKind.Create,
            TextVersionKind.Replace => IntentEventTextChangeDtoKind.Replace,
            TextVersionKind.Insert => IntentEventTextChangeDtoKind.Insert,
            _ => throw new InvalidOperationException($"Unknown text change kind: {change.Kind}"),
        },
        Snapshot = change.Snapshot,
        Old_text = change.OldText,
        New_text = change.NewText,
        After_line = change.AfterLine ?? 0,
        Insert_text = change.InsertText,
    };

    private static IntentEventLinkPayloadDto ToLinkPayload(IntentEventLinkPayload link) => new()
    {
        Id = link.Id,
        From_id = link.FromId,
        To_id = link.ToId,
        Blocking = link.Blocking,
        Author = link.Author switch
        {
            Throne.Domain.Intents.Linking.IntentLinkAuthor.User => ContractIntentLinkAuthor.User,
            Throne.Domain.Intents.Linking.IntentLinkAuthor.Agent => ContractIntentLinkAuthor.Agent,
            _ => throw new InvalidOperationException($"Unknown link author: {link.Author}"),
        },
        Rationale = link.Rationale,
        Created_at = link.CreatedAt,
    };

    private static IntentEventDtoCreated_by ToCreatedBy(IntentEventAuthor? author) => author switch
    {
        IntentEventAuthor.User => IntentEventDtoCreated_by.User,
        IntentEventAuthor.Agent => IntentEventDtoCreated_by.Agent,
        IntentEventAuthor.System => IntentEventDtoCreated_by.System,
        null => IntentEventDtoCreated_by.System,
        _ => throw new InvalidOperationException($"Unknown event author: {author}"),
    };
}
