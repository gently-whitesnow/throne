using System.Text.Json.Serialization;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record SearchIntentTextQuery(string IntentId, string Query, int? ContextLines, int? Limit);

public sealed record TextSearchResult(
    IReadOnlyList<TextSearchMatch> Matches,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? TotalMatchesEstimate);

public sealed class SearchIntentTextHandler(IIntentRepository repository)
{
    public const int DefaultContextLines = 3;
    public const int DefaultLimit = 10;

    public async Task<TextSearchResult> HandleAsync(SearchIntentTextQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Query);
        if (query.Query.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "query must not be empty.",
                new Dictionary<string, object?> { ["field"] = "query" });
        }

        var intent = await repository.GetByIdAsync(new IntentId(query.IntentId), ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        var contextLines = query.ContextLines ?? DefaultContextLines;
        var limit = Math.Min(query.Limit ?? DefaultLimit, IntentTextSearch.ServerMaxLimit);

        var result = IntentTextSearch.Search(intent.State.Text, query.Query, contextLines, limit);

        int? estimate = result.TotalMatches > result.Matches.Count ? result.TotalMatches : null;
        return new TextSearchResult(result.Matches, estimate);
    }
}
