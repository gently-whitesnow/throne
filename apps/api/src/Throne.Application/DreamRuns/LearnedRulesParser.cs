namespace Throne.Application.DreamRuns;

/// <summary>
/// Reads the <c>## Learned rules</c> section produced by <see cref="LearnedRulesInjector"/>
/// back into a structured list. Used by run_dream to feed the agent
/// <c>existing_learned_rules_by_kind</c> so it can detect duplicates without
/// pulling each user instruction separately.
/// </summary>
public static class LearnedRulesParser
{
    public static IReadOnlyList<LearnedRule> Parse(string instructionText)
    {
        ArgumentNullException.ThrowIfNull(instructionText);
        if (instructionText.Length == 0)
        {
            return [];
        }

        var headerIndex = instructionText.IndexOf(LearnedRulesInjector.SectionHeader, StringComparison.Ordinal);
        if (headerIndex < 0)
        {
            return [];
        }

        var afterHeader = instructionText.IndexOf('\n', headerIndex);
        if (afterHeader < 0)
        {
            return [];
        }

        var body = instructionText[(afterHeader + 1)..];
        var lines = body.Split('\n');
        var rules = new List<LearnedRule>();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                break;
            }
            if (!line.StartsWith("- ", StringComparison.Ordinal))
            {
                continue;
            }
            var ruleText = line[2..].Trim();
            if (ruleText.Length == 0)
            {
                continue;
            }
            rules.Add(new LearnedRule(ruleText));
        }
        return rules;
    }
}

public sealed record LearnedRule(string RuleText);
