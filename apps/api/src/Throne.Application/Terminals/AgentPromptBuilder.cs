namespace Throne.Application.Terminals;

/// <summary>
/// Builds the agent prompt for <c>tmux new -ADs throne-{intent_id} -- claude "{prompt}"</c>.
/// Hardcoded format per Slice 2 Q8 — MiniRouter relies on the exact «Прочитай бандл {mode}»
/// phrase (see memory <c>feedback_throne_bundle_prompt</c>). No textarea override.
/// </summary>
public static class AgentPromptBuilder
{
    public static string Build(string mode, string intentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

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
