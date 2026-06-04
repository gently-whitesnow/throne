namespace Throne.Application.Terminals;

/// <summary>
/// Builds the agent prompt for <c>tmux new -ADs throne-{intent_id} -- claude "{prompt}"</c>.
/// Hardcoded format per Slice 2 Q8 — MiniRouter relies on the exact «Прочитай бандл {mode}»
/// phrase (see memory <c>feedback_throne_bundle_prompt</c>). No textarea override.
/// </summary>
public static class AgentPromptBuilder
{
    /// <summary>Placeholder the operator erases and replaces with their own question in free mode.</summary>
    public const string FreeQuestionPlaceholder = "<твой вопрос>";

    public static string Build(string mode, string intentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        // Free mode reads no bundle — the phrase intentionally omits «бандл» so MiniRouter
        // does not lock onto a mode; the operator finishes the question themselves.
        if (mode == TerminalRunModes.Free)
        {
            return $"прочитай интент {intentId} и {FreeQuestionPlaceholder}";
        }

        var verb = mode switch
        {
            TerminalRunModes.Work => "выполни",
            TerminalRunModes.Interview => "проведи интервью по",
            TerminalRunModes.Dream => "проведи дрим по",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unknown terminal mode '{mode}'."),
        };

        return $"Прочитай бандл {mode} и {verb} интент {intentId}";
    }
}
