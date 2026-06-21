namespace Throne.Application.Terminals;

public static class SkillModeDefaultSeeds
{
    public static IReadOnlyList<SkillModeDefault> Build(ISessionSkillCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var skills = catalog.List();
        var result = new List<SkillModeDefault>(TerminalRunModes.All.Count * skills.Count);
        foreach (var mode in TerminalRunModes.All)
        {
            foreach (var skill in skills)
            {
                result.Add(new SkillModeDefault(mode, skill.Id, IsEnabled(mode, skill.Id)));
            }
        }

        return result;
    }

    private static bool IsEnabled(string mode, string skillId) =>
        (string.Equals(mode, TerminalRunModes.Interview, StringComparison.Ordinal)
            && string.Equals(skillId, SessionSkillPackageIds.IntentOperations, StringComparison.Ordinal))
        || (string.Equals(mode, TerminalRunModes.Review, StringComparison.Ordinal)
            && string.Equals(skillId, SessionSkillPackageIds.ReviewArtifact, StringComparison.Ordinal));
}
