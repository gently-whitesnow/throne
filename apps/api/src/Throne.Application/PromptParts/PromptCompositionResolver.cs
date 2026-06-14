using Throne.Application.Errors;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

/// <summary>
/// Resolves the effective embedded prompt composition for a mode (ADR-0036): mandatory
/// parts taken from the bundle (read from <c>prompt_parts</c> in manifest-include order, so
/// the embedded preview and <c>get_prompt_bundle</c> read the same source) plus
/// operator-authored optional parts with their per-mode roles.
/// </summary>
public sealed class PromptCompositionResolver(
    ISkillManifestProvider manifestProvider,
    PromptBundleResolver bundleResolver,
    IPromptPartRepository promptParts)
{
    public async Task<PromptComposition> ResolveAsync(ResolvePromptCompositionQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!PromptPartModeNames.IsKnown(query.Mode))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Mode '{query.Mode}' is not a known composition mode.",
                new Dictionary<string, object?>
                {
                    ["mode"] = query.Mode,
                    ["allowed_modes"] = PromptPartModeNames.All,
                });
        }

        var mandatory = await BuildMandatoryAsync(query.Mode, query.Contour, ct);
        var mandatoryIds = new HashSet<string>(mandatory.Select(p => p.PartId), StringComparer.Ordinal);
        var optional = await BuildOptionalAsync(query.Mode, query.SelectedPartIds, mandatoryIds, ct);

        var parts = mandatory.Concat(optional).ToArray();
        var selected = parts.Where(p => p.Selected).ToArray();
        var systemPrompt = string.Join("\n\n", selected.Select(p => p.Text.Trim()).Where(t => t.Length > 0));

        return new PromptComposition(
            query.Mode,
            parts,
            selected.Select(p => p.PartId).ToArray(),
            systemPrompt,
            query.IntentText ?? string.Empty);
    }

    private async Task<List<EffectivePart>> BuildMandatoryAsync(string mode, string contour, CancellationToken ct)
    {
        // free curates everything by hand — no mandatory parts and no manifest bundle.
        if (string.Equals(mode, PromptPartModeNames.Free, StringComparison.Ordinal))
        {
            return [];
        }

        var manifest = manifestProvider.Current;
        var bundle = BundleResolver.ResolveOrThrow(manifest, mode, contour);
        var (entries, _) = await bundleResolver.BuildAsync(bundle, ct);

        var parts = new List<EffectivePart>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            parts.Add(new EffectivePart(
                PartId: entry.PromptPartId,
                Key: entry.Key,
                Scope: entry.Scope,
                Role: PromptPartRoleNames.Mandatory,
                Order: i,
                Editable: string.Equals(entry.Scope, PromptPartScopeNames.User, StringComparison.Ordinal),
                Present: true,
                Selected: true,
                Text: entry.Text));
        }
        return parts;
    }

    private async Task<List<EffectivePart>> BuildOptionalAsync(
        string mode,
        IReadOnlyList<string>? selectedPartIds,
        HashSet<string> mandatoryIds,
        CancellationToken ct)
    {
        var all = await promptParts.ListAsync(PromptPartScopeNames.User, ct);
        var selection = selectedPartIds is null ? null : new HashSet<string>(selectedPartIds, StringComparer.Ordinal);

        var parts = new List<EffectivePart>();
        foreach (var part in all)
        {
            if (mandatoryIds.Contains(part.Id.Value))
            {
                continue;
            }
            var role = part.ModeRoles.FirstOrDefault(r => string.Equals(r.Mode, mode, StringComparison.Ordinal));
            if (role is null)
            {
                continue;
            }
            parts.Add(new EffectivePart(
                PartId: part.Id.Value,
                Key: part.Key,
                Scope: part.Scope,
                Role: role.Role,
                Order: role.Order,
                Editable: true,
                Present: true,
                Selected: IsSelected(role.Role, part.Id.Value, selection),
                Text: part.Text));
        }
        return parts
            .OrderBy(p => p.Order)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsSelected(string role, string partId, HashSet<string>? selection)
    {
        if (string.Equals(role, PromptPartRoleNames.Mandatory, StringComparison.Ordinal))
        {
            return true;
        }
        return selection is null
            ? string.Equals(role, PromptPartRoleNames.DefaultOn, StringComparison.Ordinal)
            : selection.Contains(partId);
    }
}
