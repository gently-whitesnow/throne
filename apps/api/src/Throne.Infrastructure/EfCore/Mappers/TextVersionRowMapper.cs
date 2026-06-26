using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class TextVersionRowMapper
{
    public static TextVersionRow ToRow(TextVersion v) => new()
    {
        Id = v.Id,
        OwnerKind = v.OwnerKind.ToWire(),
        OwnerId = v.OwnerId,
        Version = v.Version,
        Kind = v.Kind.ToWire(),
        Snapshot = v.Delta.Snapshot,
        OldText = v.Delta.OldText,
        NewText = v.Delta.NewText,
        AfterLine = v.Delta.AfterLine,
        InsertText = v.Delta.InsertText,
        ChangedAt = v.ChangedAt,
        ChangedBy = v.ChangedBy.ToWire(),
    };

    public static TextVersion ToDomain(TextVersionRow row) => new(
        Id: row.Id,
        OwnerKind: EfEnumWires.ParseTextVersionOwnerKind(row.OwnerKind),
        OwnerId: row.OwnerId,
        Version: row.Version,
        Kind: EfEnumWires.ParseTextVersionKind(row.Kind),
        Delta: new TextVersionDelta(row.Snapshot, row.OldText, row.NewText, row.AfterLine, row.InsertText),
        ChangedAt: row.ChangedAt,
        ChangedBy: EfEnumWires.ParseTextVersionAuthor(row.ChangedBy));
}
