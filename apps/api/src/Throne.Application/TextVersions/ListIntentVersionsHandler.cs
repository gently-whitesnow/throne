using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.TextVersions;

namespace Throne.Application.TextVersions;

public sealed record ListIntentVersionsQuery(string IntentId);

/// <summary>
/// Reads the linear text-only history of an Intent. Backed by <c>intent_events</c> as
/// of ADR-0019; the legacy <c>text_versions</c> collection is left as a cold backup
/// for intents and only still written to for instructions.
/// </summary>
public sealed class ListIntentVersionsHandler(
    IIntentRepository intents,
    IIntentEventRepository intentEvents)
{
    public async Task<IReadOnlyList<TextVersion>> HandleAsync(ListIntentVersionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = new IntentId(query.IntentId);
        _ = await intents.GetByIdAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        var events = await intentEvents.ListTextChangesAsync(id, ct);
        var result = new List<TextVersion>(events.Count);
        foreach (var e in events)
        {
            if (e.Kind != IntentEventKind.TextChanged || e.TextChange is null || e.Version is null)
            {
                continue;
            }

            result.Add(new TextVersion(
                Id: e.Id,
                OwnerKind: TextVersionOwnerKind.Intent,
                OwnerId: id.Value,
                Version: e.Version.Value,
                Kind: e.TextChange.Kind,
                Delta: new TextVersionDelta(
                    Snapshot: e.TextChange.Snapshot,
                    OldText: e.TextChange.OldText,
                    NewText: e.TextChange.NewText,
                    AfterLine: e.TextChange.AfterLine,
                    InsertText: e.TextChange.InsertText),
                ChangedAt: e.Audit.CreatedAt,
                ChangedBy: e.Audit.CreatedBy switch
                {
                    IntentEventAuthor.User => TextVersionAuthor.User,
                    IntentEventAuthor.Agent => TextVersionAuthor.Agent,
                    IntentEventAuthor.System => TextVersionAuthor.System,
                    _ => TextVersionAuthor.System,
                }));
        }
        return result;
    }
}
