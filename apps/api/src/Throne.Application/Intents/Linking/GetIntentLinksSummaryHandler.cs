using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents.Linking;

public sealed record GetIntentLinksSummaryQuery(IReadOnlyList<string> IntentIds);

public sealed record IntentLinksSummary(
    string IntentId,
    IReadOnlyList<Intent> BlockedBy,
    IReadOnlyList<Intent> Blocks,
    IReadOnlyList<Intent> LinkedFrom,
    IReadOnlyList<Intent> LinkedTo);

/// <summary>
/// Aggregates link roles surfaced on board cards for a batch of intents in one round-trip.
/// Companion to <see cref="ListIntentsHandler"/> — keeps the list DTO free of graph
/// joins so list refresh and links refresh can be invalidated independently.
/// </summary>
public sealed class GetIntentLinksSummaryHandler(IIntentLinkRepository repository)
{
    public const int MaxIds = 200;

    public async Task<IReadOnlyList<IntentLinksSummary>> HandleAsync(
        GetIntentLinksSummaryQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureValid(query);

        var ids = query.IntentIds
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => new IntentId(s))
            .ToList();

        var grouped = await repository.ListByIntentsAsync(ids, ct);

        var result = new List<IntentLinksSummary>(grouped.Count);
        foreach (var (intentId, views) in grouped)
        {
            result.Add(IntentLinksSummaryProjection.Build(intentId, views));
        }
        return result;
    }

    private static void EnsureValid(GetIntentLinksSummaryQuery query)
    {
        if (query.IntentIds.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "ids must contain at least one intent id.",
                new Dictionary<string, object?> { ["field"] = "ids" });
        }
        if (query.IntentIds.Count > MaxIds)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"ids must contain at most {MaxIds} intent ids per request.",
                new Dictionary<string, object?> { ["field"] = "ids", ["max"] = MaxIds });
        }
    }
}
