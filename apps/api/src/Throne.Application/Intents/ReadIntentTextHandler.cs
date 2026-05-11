using System.Text.Json.Serialization;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record ReadIntentTextQuery(string IntentId, int? StartLine, int? LineCount, int? MaxChars);

public sealed record TextSlice(
    int CurrentVersion,
    int StartLine,
    int EndLine,
    int TotalLines,
    string Content,
    bool Truncated,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? NextStartLine);

public sealed class ReadIntentTextHandler(IIntentRepository repository)
{
    public const int ServerMaxChars = IntentTextSlicer.ServerMaxChars;

    public async Task<TextSlice> HandleAsync(ReadIntentTextQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var intent = await repository.GetByIdAsync(new IntentId(query.IntentId), ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        return IntentTextSlicer.Slice(
            intent.CurrentVersion,
            intent.Text,
            query.StartLine,
            query.LineCount,
            query.MaxChars);
    }
}
