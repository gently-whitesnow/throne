using Throne.Application.Manifest;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

/// <summary>
/// Derives the mandatory per-mode roles a <c>(scope, key)</c> prompt part must carry, from the
/// manifest's <c>bundles[].includes</c>. Shared by the startup seeder and the patch apply path
/// (lazy-create of a user part), so a newly created part lands in the right bundles by
/// construction (ADR-0036).
/// </summary>
public static class PromptPartManifestRoles
{
    public static IReadOnlyList<PromptPartModeRole> MandatoryRolesFor(
        string scope, string key, SkillManifest manifest)
    {
        // A part may sit in several bundles of the same mode (one per contour, ADR-0034). A part
        // carries at most one role per mode, so we keep the first occurrence and dedup by mode.
        var roles = new List<PromptPartModeRole>();
        var seenModes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var bundle in manifest.Bundles)
        {
            for (var i = 0; i < bundle.Includes.Count; i++)
            {
                var inc = bundle.Includes[i];
                if (string.Equals(inc.Scope, scope, StringComparison.Ordinal)
                    && string.Equals(inc.Kind, key, StringComparison.Ordinal))
                {
                    if (seenModes.Add(bundle.Mode))
                    {
                        roles.Add(new PromptPartModeRole(bundle.Mode, PromptPartRoleNames.Mandatory, i));
                    }
                    break;
                }
            }
        }
        return roles;
    }
}
