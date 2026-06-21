using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed class SessionSkillSelectionService(
    ISessionSkillCatalog catalog,
    ISkillModeDefaultStore defaults,
    IIntentSkillModeSelectionStore selections)
{
    public async Task<IReadOnlyList<AvailableSessionSkill>> PreviewAsync(
        string intentId,
        string mode,
        IReadOnlyList<IntentRepositoryBinding> bindings,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(bindings);

        var defaultMap = await DefaultMapAsync(mode, ct);
        var remembered = await selections.GetAsync(intentId, mode, ct);
        var selected = remembered is null ? null : new HashSet<string>(remembered, StringComparer.Ordinal);

        return catalog.List().Select(skill =>
        {
            var materialization = MaterializationFor(skill.Id, bindings);
            var defaultEnabled = defaultMap.TryGetValue(skill.Id, out var enabled) && enabled;
            var requested = selected?.Contains(skill.Id) ?? defaultEnabled;
            return new AvailableSessionSkill(
                skill.Id,
                skill.Source,
                skill.Title,
                skill.Description,
                materialization.Materializable,
                materialization.Reason,
                defaultEnabled,
                materialization.Materializable && requested);
        }).ToArray();
    }

    public SessionSkillRunSelection Validate(
        IReadOnlyList<string>? selectedSkillIds,
        string? selectedBindingId,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (selectedSkillIds is null || selectedSkillIds.Count == 0)
        {
            return new SessionSkillRunSelection([], ReviewArtifact: null);
        }

        var selected = selectedSkillIds.Distinct(StringComparer.Ordinal).ToArray();
        var unknown = selected.Where(id => catalog.Find(id) is null).ToArray();
        if (unknown.Length > 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "selected_skill_ids contains unknown skills.",
                new Dictionary<string, object?> { ["unknown_skill_ids"] = unknown });
        }

        var reviewArtifact = ResolveReviewArtifact(selected, selectedBindingId, bindings);
        return new SessionSkillRunSelection(selected, reviewArtifact);
    }

    private static ReviewArtifactWriteTarget? ResolveReviewArtifact(
        IReadOnlyList<string> selected,
        string? selectedBindingId,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        if (!selected.Contains(SessionSkillPackageIds.Review, StringComparer.Ordinal))
        {
            return null;
        }

        var target = ReviewArtifactWriteTarget.Resolve(selectedBindingId, bindings);
        if (target is not null)
        {
            return target;
        }

        throw new ApiException(
            ErrorCodes.ValidationFailed,
            "selected_skill_ids contains a skill that is not materializable for this intent.",
            new Dictionary<string, object?>
            {
                ["skill_id"] = SessionSkillPackageIds.Review,
                ["reason"] = ReviewArtifactWriteTarget.NoBindingReason,
            });
    }

    private async Task<Dictionary<string, bool>> DefaultMapAsync(string mode, CancellationToken ct)
    {
        var seeded = SkillModeDefaultSeeds.Build(catalog)
            .Where(d => string.Equals(d.Mode, mode, StringComparison.Ordinal))
            .ToDictionary(d => d.SkillId, d => d.Enabled, StringComparer.Ordinal);
        var stored = await defaults.ListAsync(ct);
        foreach (var item in stored)
        {
            if (string.Equals(item.Mode, mode, StringComparison.Ordinal)
                && seeded.ContainsKey(item.SkillId))
            {
                seeded[item.SkillId] = item.Enabled;
            }
        }

        return seeded;
    }

    private static SkillMaterialization MaterializationFor(
        string skillId,
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        if (string.Equals(skillId, SessionSkillPackageIds.Review, StringComparison.Ordinal)
            && bindings.Count == 0)
        {
            return new SkillMaterialization(false, ReviewArtifactWriteTarget.NoBindingReason);
        }

        return new SkillMaterialization(true, Reason: null);
    }

    private sealed record SkillMaterialization(bool Materializable, string? Reason);
}
