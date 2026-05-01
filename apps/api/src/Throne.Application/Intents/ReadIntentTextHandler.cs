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
    public const int ServerMaxChars = 64_000;

    public async Task<TextSlice> HandleAsync(ReadIntentTextQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var intent = await repository.GetByIdAsync(new IntentId(query.IntentId), ct).ConfigureAwait(false)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        var lines = SplitLines(intent.Text);
        var totalLines = lines.Length;
        var startLine = query.StartLine ?? 1;
        if (startLine < 1)
        {
            startLine = 1;
        }

        if (totalLines == 0)
        {
            return new TextSlice(intent.CurrentVersion, StartLine: 1, EndLine: 0, TotalLines: 0,
                Content: string.Empty, Truncated: false, NextStartLine: null);
        }

        if (startLine > totalLines)
        {
            return new TextSlice(intent.CurrentVersion, StartLine: startLine, EndLine: startLine - 1,
                TotalLines: totalLines, Content: string.Empty, Truncated: false, NextStartLine: null);
        }

        var requested = query.LineCount ?? (totalLines - startLine + 1);
        if (requested < 0)
        {
            requested = 0;
        }

        var charLimit = Math.Min(query.MaxChars ?? ServerMaxChars, ServerMaxChars);

        var sb = new System.Text.StringBuilder();
        var endLine = startLine - 1;
        var truncated = false;
        for (var i = 0; i < requested && (startLine - 1 + i) < totalLines; i++)
        {
            var line = lines[startLine - 1 + i];
            var addition = i == 0 ? line : "\n" + line;
            if (sb.Length + addition.Length > charLimit)
            {
                truncated = true;
                break;
            }

            sb.Append(addition);
            endLine = startLine + i;
        }

        int? nextStartLine = truncated && endLine < totalLines ? endLine + 1 : null;

        return new TextSlice(
            CurrentVersion: intent.CurrentVersion,
            StartLine: startLine,
            EndLine: endLine,
            TotalLines: totalLines,
            Content: sb.ToString(),
            Truncated: truncated,
            NextStartLine: nextStartLine);
    }

    private static string[] SplitLines(string text) =>
        text.Length == 0 ? [] : text.Split('\n');
}
