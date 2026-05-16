using Throne.Domain.Intents.Linking;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents.Events;

/// <summary>
/// Append-only event in the canonical history of an Intent (ADR-0019).
/// Stage-2 kinds: <c>text_changed</c>, <c>link_added</c>, <c>link_removed</c>.
/// Audit-фасет (when/by) свёрнут в <see cref="IntentEventAudit"/>, чтобы primary
/// constructor оставался в пределах CTOR_DEPS-бюджета (8 параметров).
/// </summary>
public sealed record IntentEvent(
    string Id,
    IntentId IntentId,
    IntentId? PeerIntentId,
    IntentEventKind Kind,
    int? Version,
    IntentEventTextChange? TextChange,
    IntentEventLinkPayload? Link,
    IntentEventAudit Audit)
{
    public static IntentEvent ForText(
        string id,
        IntentId intentId,
        TextVersion version,
        DateTimeOffset createdAt) =>
        new(
            Id: id,
            IntentId: intentId,
            PeerIntentId: null,
            Kind: IntentEventKind.TextChanged,
            Version: version.Version,
            TextChange: IntentEventTextChange.From(version),
            Link: null,
            Audit: new IntentEventAudit(createdAt, AuthorFromTextVersion(version.ChangedBy)));

    public static IntentEvent ForLinkAdded(string id, IntentLink link) =>
        new(
            Id: id,
            IntentId: link.FromId,
            PeerIntentId: link.ToId,
            Kind: IntentEventKind.LinkAdded,
            Version: null,
            TextChange: null,
            Link: IntentEventLinkPayload.From(link),
            Audit: new IntentEventAudit(link.CreatedAt, AuthorFromLinkAuthor(link.Author)));

    public static IntentEvent ForLinkRemoved(string id, IntentLink link, DateTimeOffset removedAt) =>
        new(
            Id: id,
            IntentId: link.FromId,
            PeerIntentId: link.ToId,
            Kind: IntentEventKind.LinkRemoved,
            Version: null,
            TextChange: null,
            Link: IntentEventLinkPayload.From(link),
            Audit: new IntentEventAudit(removedAt, null));

    private static IntentEventAuthor AuthorFromTextVersion(TextVersionAuthor author) => author switch
    {
        TextVersionAuthor.User => IntentEventAuthor.User,
        TextVersionAuthor.Agent => IntentEventAuthor.Agent,
        TextVersionAuthor.System => IntentEventAuthor.System,
        _ => throw new InvalidOperationException($"Unknown text version author: {author}."),
    };

    private static IntentEventAuthor AuthorFromLinkAuthor(IntentLinkAuthor author) => author switch
    {
        IntentLinkAuthor.User => IntentEventAuthor.User,
        IntentLinkAuthor.Agent => IntentEventAuthor.Agent,
        _ => throw new InvalidOperationException($"Unknown link author: {author}."),
    };
}

public sealed record IntentEventAudit(DateTimeOffset CreatedAt, IntentEventAuthor? CreatedBy);

public enum IntentEventKind
{
    TextChanged = 1,
    LinkAdded = 2,
    LinkRemoved = 3,
}

public enum IntentEventAuthor
{
    User = 1,
    Agent = 2,
    System = 3,
}

public sealed record IntentEventTextChange(
    TextVersionKind Kind,
    string? Snapshot,
    string? OldText,
    string? NewText,
    int? AfterLine,
    string? InsertText)
{
    public static IntentEventTextChange From(TextVersion v) => new(
        Kind: v.Kind,
        Snapshot: v.Delta.Snapshot,
        OldText: v.Delta.OldText,
        NewText: v.Delta.NewText,
        AfterLine: v.Delta.AfterLine,
        InsertText: v.Delta.InsertText);
}

public sealed record IntentEventLinkPayload(
    string Id,
    string FromId,
    string ToId,
    string Type,
    IntentLinkAuthor Author,
    string? Rationale,
    DateTimeOffset CreatedAt)
{
    public static IntentEventLinkPayload From(IntentLink link) => new(
        Id: link.Id,
        FromId: link.FromId.Value,
        ToId: link.ToId.Value,
        Type: link.Type,
        Author: link.Author,
        Rationale: link.Rationale,
        CreatedAt: link.CreatedAt);
}

public static class IntentEventKindExtensions
{
    public static string ToWire(this IntentEventKind kind) => kind switch
    {
        IntentEventKind.TextChanged => "text_changed",
        IntentEventKind.LinkAdded => "link_added",
        IntentEventKind.LinkRemoved => "link_removed",
        _ => throw new InvalidOperationException($"Unknown kind: {kind}."),
    };

    public static IntentEventKind FromWire(string wire) => wire switch
    {
        "text_changed" => IntentEventKind.TextChanged,
        "link_added" => IntentEventKind.LinkAdded,
        "link_removed" => IntentEventKind.LinkRemoved,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown intent_event kind."),
    };

    public static string ToWire(this IntentEventAuthor author) => author switch
    {
        IntentEventAuthor.User => "user",
        IntentEventAuthor.Agent => "agent",
        IntentEventAuthor.System => "system",
        _ => throw new InvalidOperationException($"Unknown author: {author}."),
    };

    public static IntentEventAuthor AuthorFromWire(string wire) => wire switch
    {
        "user" => IntentEventAuthor.User,
        "agent" => IntentEventAuthor.Agent,
        "system" => IntentEventAuthor.System,
        _ => throw new ArgumentOutOfRangeException(nameof(wire), wire, "Unknown intent_event author."),
    };
}
