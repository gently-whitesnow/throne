using Throne.Application.Ports;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Per-intent breakdown produced by <see cref="ContextTokenCounter"/>.
/// </summary>
public sealed record IntentTokenBreakdown(string IntentId, int TokenCount, DateTimeOffset UpdatedAt);

public sealed record ContextTokenization(int TotalTokens, IReadOnlyList<IntentTokenBreakdown> PerIntent);

/// <summary>
/// Counts tokens of /dream training context per intent and in total. Concatenates
/// `text_versions` (Version ASC), the final `Intent.text` (deduplicated against the last
/// version snapshot), then all `intent_qa` and `intent_review` (CreatedAt ASC).
/// Attachments are NOT included — out of scope (binary blobs).
/// </summary>
public sealed class ContextTokenCounter(ITokenizer tokenizer)
{
    private readonly ITokenizer _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));

    public ContextTokenization Count(IntentWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        var perIntent = new List<IntentTokenBreakdown>(window.Items.Count);
        var total = 0;
        foreach (var intent in window.Items)
        {
            var tokens = CountForIntent(intent);
            total += tokens;
            perIntent.Add(new IntentTokenBreakdown(intent.IntentId, tokens, intent.UpdatedAt));
        }
        return new ContextTokenization(total, perIntent);
    }

    private int CountForIntent(IntentInWindow intent)
    {
        var parts = new List<string>(intent.TextVersions.Count + intent.QaList.Count + intent.ReviewList.Count + 1);
        string? lastVersionText = null;
        foreach (var v in intent.TextVersions.OrderBy(v => v.Version))
        {
            var text = v.EffectiveText();
            parts.Add(text);
            lastVersionText = text;
        }

        // Дедуп: финальный Intent.text не добавляем, если он byte-equal последнему snapshot/new_text/insert_text.
        if (!string.Equals(intent.CurrentText, lastVersionText, StringComparison.Ordinal))
        {
            parts.Add(intent.CurrentText);
        }

        foreach (var qa in intent.QaList.OrderBy(q => q.CreatedAt))
        {
            parts.Add($"Q: {qa.Question}\nA: {qa.Answer}");
        }
        foreach (var r in intent.ReviewList.OrderBy(r => r.CreatedAt))
        {
            parts.Add($"Reason: {r.Reason}\nNote: {r.Note}");
        }

        if (parts.Count == 0)
        {
            return 0;
        }
        return _tokenizer.CountTokens(string.Join("\n", parts));
    }
}
