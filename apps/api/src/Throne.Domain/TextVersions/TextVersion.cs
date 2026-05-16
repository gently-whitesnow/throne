namespace Throne.Domain.TextVersions;

public sealed record TextVersion(
    string Id,
    TextVersionOwnerKind OwnerKind,
    string OwnerId,
    int Version,
    TextVersionKind Kind,
    TextVersionDelta Delta,
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
            Delta: new TextVersionDelta(snapshot, null, null, null, null),
            changedAt, changedBy);
}
