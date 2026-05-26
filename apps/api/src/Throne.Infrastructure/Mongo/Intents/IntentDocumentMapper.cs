using Throne.Application.Auth;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal static class IntentDocumentMapper
{
    public static IntentDocument ToDocument(Intent intent) => new()
    {
        Id = intent.Id.Value,
        OwnerUserId = intent.OwnerUserId,
        Text = intent.State.Text,
        Status = intent.State.Status,
        CurrentVersion = intent.State.CurrentVersion,
        TagIds = intent.TagIds.Select(t => t.Value).ToList(),
        SortKey = intent.State.SortKey,
        CreatedAt = intent.CreatedAt.UtcDateTime,
        UpdatedAt = intent.State.UpdatedAt.UtcDateTime,
    };

    public static IntentStatusChangeDocument ToDocument(IntentStatusChange change) => new()
    {
        Id = change.Id,
        IntentId = change.IntentId.Value,
        IntentVersionAtWrite = change.IntentVersionAtWrite,
        FromStatus = change.FromStatus,
        ToStatus = change.ToStatus,
        Source = change.Source,
        CreatedAt = change.Audit.CreatedAt.UtcDateTime,
        CreatedBy = change.Audit.CreatedBy.ToWire(),
        Reason = change.Reason,
    };

    public static Intent ToDomain(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        ownerUserId: string.IsNullOrWhiteSpace(doc.OwnerUserId) ? CurrentUserIds.LocalDev : doc.OwnerUserId,
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        sortKey: doc.SortKey,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));

    public static TextVersionAuthor ToTextVersionAuthor(IntentTrainingAuthor author) => author switch
    {
        IntentTrainingAuthor.User => TextVersionAuthor.User,
        IntentTrainingAuthor.Agent => TextVersionAuthor.Agent,
        IntentTrainingAuthor.System => TextVersionAuthor.System,
        _ => throw new InvalidOperationException($"Unknown training author: {author}."),
    };
}
