using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.Tags;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal static class MongoIntentLinkMapper
{
    public static IntentLinkDocument ToDocument(IntentLink link) => new()
    {
        Id = link.Id,
        FromId = link.FromId.Value,
        ToId = link.ToId.Value,
        Blocking = link.Blocking,
        Author = link.Author.ToWire(),
        Rationale = link.Rationale,
        CreatedAt = link.CreatedAt.UtcDateTime,
    };

    public static IntentLink ToDomain(IntentLinkDocument doc) => new(
        Id: doc.Id,
        FromId: new IntentId(doc.FromId),
        ToId: new IntentId(doc.ToId),
        Blocking: doc.Blocking,
        Author: IntentLinkAuthorExtensions.FromWire(doc.Author),
        Rationale: doc.Rationale,
        CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)));

    public static Intent IntentToDomain(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        sortKey: doc.SortKey,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));
}
