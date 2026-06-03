using Throne.Application.Auth;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Mongo;

internal static class MongoTagMapper
{
    public static TagDocument ToDocument(Tag tag) => new()
    {
        Id = tag.Id.Value,
        Name = tag.Name,
        CurrentVersion = tag.CurrentVersion,
        CreatedAt = tag.CreatedAt.UtcDateTime,
        UpdatedAt = tag.UpdatedAt.UtcDateTime,
        DefaultRepositories = tag.DefaultRepositories.Count == 0
            ? []
            : [.. tag.DefaultRepositories.Select(DefaultRepositoryToDocument)],
    };

    public static Tag ToDomain(TagDocument doc) => Tag.Restore(
        id: new TagId(doc.Id),
        name: doc.Name,
        currentVersion: doc.CurrentVersion,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc),
        defaultRepositories: doc.DefaultRepositories.Count == 0
            ? []
            : [.. doc.DefaultRepositories.Select(DefaultRepositoryToDomain)]);

    public static TagDefaultRepositoryDocument DefaultRepositoryToDocument(TagDefaultRepository entry) => new()
    {
        Provider = entry.Coordinate.Provider,
        Owner = entry.Coordinate.Owner,
        Repo = entry.Coordinate.Repo,
        DefaultBranch = entry.DefaultBranch,
    };

    public static Intent IntentToDomain(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        ownerUserId: string.IsNullOrWhiteSpace(doc.OwnerUserId)
            ? CurrentUserIds.LocalDev
            : doc.OwnerUserId,
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        sortKey: doc.SortKey,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));

    private static TagDefaultRepository DefaultRepositoryToDomain(TagDefaultRepositoryDocument doc) =>
        new(new RepoCoordinate(doc.Provider, doc.Owner, doc.Repo), doc.DefaultBranch);
}
