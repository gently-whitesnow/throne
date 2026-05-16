using Throne.Application.TextVersions;
using Throne.Domain.TextVersions;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class TextVersionDtoMapper
{
    public static TextVersionDto ToDto(TextVersion v) => new()
    {
        Version = v.Version,
        Kind = MapKind(v.Kind),
        Changed_at = v.ChangedAt,
        Changed_by = MapAuthor(v.ChangedBy),
        Snapshot = v.Delta.Snapshot,
        Old_text = v.Delta.OldText,
        New_text = v.Delta.NewText,
        After_line = v.Delta.AfterLine ?? 0,
        Insert_text = v.Delta.InsertText,
    };

    private static TextVersionDtoKind MapKind(TextVersionKind kind) => kind switch
    {
        TextVersionKind.Create => TextVersionDtoKind.Create,
        TextVersionKind.Replace => TextVersionDtoKind.Replace,
        TextVersionKind.Insert => TextVersionDtoKind.Insert,
        _ => throw new InvalidOperationException($"Unknown kind: {kind}"),
    };

    private static TextVersionDtoChanged_by MapAuthor(TextVersionAuthor author) => author switch
    {
        TextVersionAuthor.User => TextVersionDtoChanged_by.User,
        TextVersionAuthor.Agent => TextVersionDtoChanged_by.Agent,
        TextVersionAuthor.System => TextVersionDtoChanged_by.System,
        _ => throw new InvalidOperationException($"Unknown author: {author}"),
    };
}
