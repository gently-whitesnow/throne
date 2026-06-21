using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Application.Terminals;

public sealed class SkillModeDefaultsService(
    ISessionSkillCatalog catalog,
    ISkillModeDefaultStore store,
    IUnitOfWork unitOfWork)
{
    public async Task<SkillModeDefaultsView> GetAsync(CancellationToken ct)
    {
        var stored = await store.ListAsync(ct);
        return BuildView(stored);
    }

    public async Task<SkillModeDefaultsView> ReplaceAsync(
        IReadOnlyList<SkillModeDefault> defaults,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        var normalized = Normalize(defaults);
        await unitOfWork.ExecuteAsync(inner => store.ReplaceAsync(normalized, inner), ct);
        return BuildView(normalized);
    }

    private SkillModeDefaultsView BuildView(IReadOnlyList<SkillModeDefault> stored)
    {
        var skills = catalog.List();
        var effective = SkillModeDefaultSeeds.Build(catalog).ToDictionary(KeyOf, StringComparer.Ordinal);
        foreach (var item in stored)
        {
            if (TerminalRunModes.IsKnown(item.Mode) && catalog.Find(item.SkillId) is not null)
            {
                effective[KeyOf(item)] = item;
            }
        }

        var modes = TerminalRunModes.All
            .Select(mode => new SkillModeDefaultsModeView(
                mode,
                skills.Select(skill =>
                {
                    var enabled = effective[KeyOf(mode, skill.Id)].Enabled;
                    return new SkillModeDefaultState(skill.Id, enabled);
                }).ToArray()))
            .ToArray();
        return new SkillModeDefaultsView(skills, modes);
    }

    private SkillModeDefault[] Normalize(IReadOnlyList<SkillModeDefault> defaults)
    {
        var result = new Dictionary<string, SkillModeDefault>(StringComparer.Ordinal);
        foreach (var item in defaults)
        {
            if (!TerminalRunModes.IsKnown(item.Mode))
            {
                throw Validation("Unknown skill mode.", "mode", item.Mode);
            }
            if (catalog.Find(item.SkillId) is null)
            {
                throw Validation("Unknown session skill.", "skill_id", item.SkillId);
            }
            result[KeyOf(item)] = item;
        }

        return result.Values
            .OrderBy(d => ModeOrder(d.Mode))
            .ThenBy(d => d.SkillId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ApiException Validation(string message, string key, string value) =>
        new(
            ErrorCodes.ValidationFailed,
            message,
            new Dictionary<string, object?> { [key] = value });

    private static string KeyOf(SkillModeDefault item) => KeyOf(item.Mode, item.SkillId);

    private static string KeyOf(string mode, string skillId) => $"{mode}:{skillId}";

    private static int ModeOrder(string mode)
    {
        for (var i = 0; i < TerminalRunModes.All.Count; i++)
        {
            if (string.Equals(TerminalRunModes.All[i], mode, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
