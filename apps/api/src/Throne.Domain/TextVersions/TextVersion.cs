namespace Throne.Domain.TextVersions;

public sealed record TextVersion(
    string Id,
    TextVersionOwnerKind OwnerKind,
    string OwnerId,
    int Version,
    TextVersionKind Kind,
    string? Snapshot,
    string? OldText,
    string? NewText,
    int? AfterLine,
    string? InsertText,
    DateTimeOffset ChangedAt,
    TextVersionAuthor ChangedBy)
{
    public static TextVersion CreateSnapshot(
        string id,
        TextVersionOwnerKind ownerKind,
        string ownerId,
        string snapshot,
        DateTimeOffset changedAt,
        TextVersionAuthor changedBy) =>
        new(id, ownerKind, ownerId, Version: 1, TextVersionKind.Create,
            Snapshot: snapshot,
            OldText: null, NewText: null, AfterLine: null, InsertText: null,
            changedAt, changedBy);
}
