using Throne.Application.Errors;
using Throne.Application.Instructions;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

/// <summary>
/// Resolves the effective embedded prompt composition for a mode (ADR-0035): mandatory
/// parts projected from the skill manifest (so the MCP bundle and embedded preview read
/// the same instruction text) plus operator-authored optional parts with their per-mode
/// roles. The MCP <c>get_instruction_bundle</c> path is untouched — this is an additive
/// projection over the same sources.
/// </summary>
public sealed class PromptCompositionResolver(
    ISkillManifestProvider manifestProvider,
    UserBundleEntries userEntries,
    IPromptPartRepository promptParts)
{
    public async Task<PromptComposition> ResolveAsync(ResolvePromptCompositionQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!PromptPartModeNames.IsKnown(query.Mode))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                $"Mode '{query.Mode}' is not an embedded composition mode.",
                new Dictionary<string, object?>
                {
                    ["mode"] = query.Mode,
                    ["allowed_modes"] = PromptPartModeNames.All,
                });
        }

        var mandatory = await BuildMandatoryAsync(query.Mode, ct);
        var optional = await BuildOptionalAsync(query.Mode, query.SelectedPartIds, ct);

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

    private async Task<List<EffectivePart>> BuildMandatoryAsync(string mode, CancellationToken ct)
    {
        // free curates everything by hand — no mandatory parts.
        if (string.Equals(mode, PromptPartModeNames.Free, StringComparison.Ordinal))
        {
            return [];
        }

        var manifest = manifestProvider.Current;
        var bundle = BundleResolver.ResolveOrThrow(manifest, mode);
        var (systemEntries, _) = SystemBundleEntries.Build(bundle, manifest);
        var userBuilt = await userEntries.BuildAsync(bundle, ct);

        var ordered = systemEntries.Concat(userBuilt).ToList();
        var parts = new List<EffectivePart>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            parts.Add(new EffectivePart(
                PartId: entry.InstructionId,
                Key: entry.Kind,
                Scope: entry.Scope,
                Role: PromptPartRoleNames.Mandatory,
                Order: i,
                Editable: string.Equals(entry.Scope, InstructionScopeNames.User, StringComparison.Ordinal),
                Present: true,
                Selected: true,
                Text: entry.Text));
        }
        return parts;
    }

    private async Task<List<EffectivePart>> BuildOptionalAsync(
        string mode,
        IReadOnlyList<string>? selectedPartIds,
        CancellationToken ct)
    {
        var all = await promptParts.ListAsync(ct);
        var selection = selectedPartIds is null ? null : new HashSet<string>(selectedPartIds, StringComparer.Ordinal);

        var parts = new List<EffectivePart>();
        foreach (var part in all)
        {
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
